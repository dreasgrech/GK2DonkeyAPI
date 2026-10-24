using System;
using System.Collections.Generic;
using UnityEngine;

namespace GK2DonkeyAPI.Api
{
    // Mounts (docs/mounts-design.md): what the player rides as a creature type (the donkey, the giant rat, the
    // dragon, ...), against a skin, which is how it is drawn (the red and the blue dragon are two skins of the dragon
    // mount). A mount owns the rider's seat for it (values for existing tuning keys, per style), whether it pulls the
    // cart and has foals, a default effect, and its abilities (MountAbility: code the API calls at the instance's
    // lifecycle points). A skin names its mount (MountSkinDef.Mount, a pack's `mount =`; the default is the donkey);
    // an instance's mount is its skin's (MountHandle.Mount), so a player picks a skin and the mount follows.
    //
    // Three ways to make a mount: here (Register, with abilities), a pack's [Mount] section in skin.cfg (no code; the
    // mount's own skin owns the declaration, a joiner's holds until the Default is registered), or none: a skin of
    // the donkey mount.
    public sealed class MountCatalog
    {
        public const string DonkeyId = MountLooks.DonkeyId;

        internal MountCatalog()
        {
        }

        // Every mount: the donkey first, then the rest by name.
        public IReadOnlyList<MountInfo> All
        {
            get
            {
                var list = new List<MountInfo>();
                foreach (GK2DonkeyAPI.Catalog.Entry d in GK2DonkeyAPI.Catalog.All) list.Add(new MountInfo(d));
                list.Sort((a, b) => a.Id == DonkeyId ? -1 : b.Id == DonkeyId ? 1 : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
                return list;
            }
        }

        public MountInfo Find(string id)
        {
            GK2DonkeyAPI.Catalog.Entry d = GK2DonkeyAPI.Catalog.Find(id);
            return d == null ? null : new MountInfo(d);
        }

        // Raised after a mount is registered, declared by a pack or withdrawn.
        public event Action MountsChanged;

        internal void RaiseChanged()
        {
            Action h = MountsChanged;
            if (h == null) return;
            foreach (Delegate d in h.GetInvocationList())
            {
                try
                {
                    ((Action)d)();
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning("Catalog: a MountsChanged handler threw: " + e.Message);
                }
            }
        }

        // A mount of your own. The id must be new (or yours already: then it replaces, and every instance of it
        // re-applies). Dispose to withdraw: its skins fall back to the donkey mount.
        public IDisposable Register(string ownerId, MountDef def)
        {
            if (string.IsNullOrWhiteSpace(ownerId)) throw new ArgumentException("An owner id (your plugin GUID) is required.");
            if (def == null) throw new ArgumentNullException(nameof(def));
            var d = new GK2DonkeyAPI.Catalog.Entry
            {
                Id = def.Id, Name = string.IsNullOrEmpty(def.Name) ? def.Id : def.Name, Noun = string.IsNullOrWhiteSpace(def.Noun) ? null : def.Noun.Trim(), Author = def.Author, Version = def.Version, Owner = ownerId,
                Cart = def.Cart, Foals = def.Foals, Flies = def.Flies, Jumps = def.Jumps, FoalScale = Math.Max(0.2f, Math.Min(1f, def.FoalScale)), Speed = ClampSpeed(def.Speed), Cargo = def.Cargo.HasValue ? Math.Max(1, Math.Min(GK2DonkeyAPI.Catalog.CargoMax, def.Cargo.Value)) : (int?)null, Effect = MountEffects.IsNone(def.Effect) ? null : def.Effect.Trim(), EffectWhen = def.EffectWhen,
                Tuning = Copy(def.Tuning), TuningRide = Copy(def.TuningRide), TuningCart = Copy(def.TuningCart),
                Abilities = def.Abilities != null ? def.Abilities.ToArray() : Array.Empty<MountAbility>(),
                Description = string.IsNullOrWhiteSpace(def.Description) ? null : def.Description.Trim(), PortraitTexture = def.Portrait,
            };
            if (def.Stats != null)
            {
                foreach (MountStat st in def.Stats)
                {
                    if (st == null || string.IsNullOrWhiteSpace(st.Label)) continue;
                    if (GK2DonkeyAPI.Catalog.ReservedStat(st.Label))
                    {
                        Plugin.Log.LogWarning($"Catalog: mount '{def.Id}': the stat \"{st.Label}\" is the API's own line (MountDef.Speed / Cargo); left out.");
                        continue;
                    }
                    d.Stats.Add(new KeyValuePair<string, string>(st.Label.Trim(), st.Value ?? ""));
                }
            }
            if ((def.Sounds?.Count ?? 0) + (def.SoundsRide?.Count ?? 0) + (def.SoundsCart?.Count ?? 0) + (def.SoundsBaby?.Count ?? 0) > 0)
            {
                d.SoundBlocks = GK2DonkeyAPI.Looks.BlocksFromApi(def.Sounds, def.SoundsRide, def.SoundsCart, def.SoundsBaby, "Mount.Sounds", "mount " + def.Id);
                d.Sounds = AsObjects(def.Sounds);
                d.SoundsRide = AsObjects(def.SoundsRide);
                d.SoundsCart = AsObjects(def.SoundsCart);
                d.SoundsBaby = AsObjects(def.SoundsBaby);
            }
            d.AbilityOwner = ownerId + ".ability." + d.Id;
            foreach (MountAbility a in d.Abilities)
            {
                if (a == null || string.IsNullOrEmpty(a.Id)) throw new ArgumentException($"Mount '{def.Id}': every ability needs an Id.", nameof(def));
                if (a.Owner != null && a.Owner != d.AbilityOwner) throw new ArgumentException($"Mount '{def.Id}': the ability '{a.Id}' already belongs to another mount ({a.Owner}); one ability object per mount.", nameof(def));
                a.Owner = d.AbilityOwner;
            }
            if (!Mounts.MountsEnabled) return Mounts.KeptOut(ownerId, "the mount", def.Id);
            GK2DonkeyAPI.Catalog.Register(d);
            return new Handle(d.Id, ownerId);
        }

        public bool Unregister(string ownerId, string id)
        {
            if (string.IsNullOrWhiteSpace(ownerId)) throw new ArgumentException("An owner id (your plugin GUID) is required.");
            return GK2DonkeyAPI.Catalog.Unregister(id, ownerId);
        }

        private static float? ClampSpeed(float? v) => v.HasValue ? Math.Max(GK2DonkeyAPI.Catalog.SpeedMin, Math.Min(GK2DonkeyAPI.Catalog.SpeedMax, v.Value)) : (float?)null;

        private static Dictionary<string, object> Copy(Dictionary<string, object> d)
        {
            return d != null && d.Count > 0 ? new Dictionary<string, object>(d, StringComparer.OrdinalIgnoreCase) : null;
        }

        private static Dictionary<string, object> AsObjects(Dictionary<string, MountSound> d)
        {
            if (d == null || d.Count == 0) return null;
            var o = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in d) o[kv.Key] = kv.Value;
            return o;
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
                GK2DonkeyAPI.Catalog.Unregister(id, owner);
            }
        }
    }

    // A line the Mounts panel shows under a mount, as given ("Speed" / "fast"; "Diet" / "keepers").
    public sealed class MountStat
    {
        public string Label;
        public string Value;
    }

    public sealed class MountAbilityInfo
    {
        internal MountAbilityInfo(MountAbility a)
        {
            Id = a.Id;
            Name = string.IsNullOrWhiteSpace(a.Name) ? a.Id : a.Name;
            Description = a.Description ?? "";
        }

        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
    }

    public sealed class MountDef
    {
        public string Id;
        public string Name;
        // The word the game's sentences use for one ("a skull can't pull the cart"); null: the name in lower case.
        public string Noun;
        public string Author;
        public string Version;
        // May pull the cart (false: the cart style, Hitch and MountCompanion.Use(Cart) are refused for this mount).
        public bool Cart = true;
        // Foals (babies) may be drawn as it.
        public bool Foals = true;
        // Takes off and flies, ridden plain (0.36.0; flying is a dev-only experiment for now, docs/flying.md). With the
        // jump and fly keys the same, a second press in the air turns a jump into the take-off.
        public bool Flies;
        // Jumps, ridden plain (0.36.0; on by default; false: the jump key does nothing on it, or takes off if it flies).
        public bool Jumps = true;
        // A baby's size as a fraction of the grown one (0.2 to 1; the donkey's foals are 0.6): a mount drawn three
        // times its creature's size (Gerry) sets 0.33 so its babies are the creature's own size.
        public float FoalScale = 0.6f;
        // How fast the keeper rides it, times his walking speed (1 = walking; 0.36.0): 0.5 to 4 (tested in game up to
        // 2.5). Null: the donkey's (2 as shipped). With the cart: this times the donkey's cart speed over its riding one
        // (1.5 / 2 as shipped). Over a SpeedMultiplier in Tuning / TuningRide / TuningCart. The Mounts panel shows it as
        // a word against the donkey's ("Fast").
        public float? Speed;
        // How many items its cart carries, 1 to 6 (0.36.0). Null: 6. A stat for the Mounts panel for now: what a cart
        // holds is still the player's Cart capacity (CartSlots).
        public int? Cargo;
        // The effect an instance of this mount shows when its own Effect key is MountEffects.FromMount ("mount"): an effect
        // id, or null for none; and when.
        public string Effect;
        public MountEffectWhen EffectWhen = MountEffectWhen.Always;
        // Values for existing tuning keys (SeatAdjustPx, RiderOffsetPx, LegAdjustYPx, SpeedMultiplier, ...), typed as the
        // key is: set on every instance of this mount as overrides owned by the skin system while it is of this mount.
        // Tuning: both styles; TuningRide / TuningCart: one style, over Tuning. A skin's own Tuning refines them.
        public Dictionary<string, object> Tuning;
        public Dictionary<string, object> TuningRide;
        public Dictionary<string, object> TuningCart;
        // The creature's sounds by event key ("voice", "step.grass": MountSound; a pack's [Mount.Sounds] block), shared by
        // every skin of the mount, under a skin's own; SoundsRide / SoundsCart for one style, over Sounds. Null: the
        // player's keys (the donkey's sounds). docs/mount-sounds-design.md.
        public Dictionary<string, MountSound> Sounds, SoundsRide, SoundsCart;
        // The babies' own sounds, over the blocks above for an instance drawn smaller than the grown one (a pack's
        // [Mount.Sounds.Baby]). Null: the grown one's, pitched up by the size.
        public Dictionary<string, MountSound> SoundsBaby;
        // For the Mounts panel (docs/mounts-foundation-2.md 3): a line or two about the creature; a portrait for the
        // list (your own texture, kept by you; null: the panel draws the mount); lines shown as given ("Diet: keepers").
        public string Description;
        public Texture2D Portrait;
        public List<MountStat> Stats = new List<MountStat>();
        // Code the API calls for every instance of this mount: see MountAbility.
        public List<MountAbility> Abilities = new List<MountAbility>();
    }

    // Behaviour a mod attaches to a mount. One object per mount, shared by every instance of it: keep per-instance state
    // in a dictionary keyed by the handle, or in handle.SetData. The API calls the hooks for instances that have a
    // handle (not the settings picture's paper donkey, not a plain summon without one). An ability that throws is
    // switched off for the session on that instance, with one warning. Keys, settings rows, sounds and effects are the
    // mod's own calls from these hooks (the settings page, Mounts.Effects, ...).
    public abstract class MountAbility
    {
        public abstract string Id { get; }

        // The owner id an ability's Tuning.Set and Clear use ("<the mount's owner>.ability.<mount id>"), given by the API
        // when the mount is registered: the API clears every value under it when the instance stops being this mount
        // (Detached) or goes, so nothing of an ability outlives its mount. An ability may set the mount's keys (speed,
        // sounds, the rider's motion, the pose, the cart's geometry), never the player's look (the skin, the effect, the
        // colours) or behaviour switches: those belong to the player's mod (docs/mounts-foundation-2.md 2.1).
        public string Owner { get; internal set; }

        // What the Mounts panel shows for it: a name (the Id unless given) and a line about what it does.
        public virtual string Name => Id;
        public virtual string Description => "";

        // Tick runs only while the instance is ridden, unless this is true (then every frame it has a view): keep it
        // false unless the ability needs to act on a standing creature.
        public virtual bool TicksWhileStanding => false;

        // The instance became this mount (spawned, loaded, or its skin changed to one of this mount).
        public virtual void Attached(MountHandle mount)
        {
        }

        // It stopped being this mount (its skin changed), or it is removed.
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

    public sealed class MountInfo
    {
        internal MountInfo(GK2DonkeyAPI.Catalog.Entry d)
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
            Flies = d.Flies;
            Jumps = d.Jumps;
            FoalScale = d.FoalScale;
            Speed = GK2DonkeyAPI.Catalog.SpeedOf(d, MountStyle.Ride);
            CartSpeed = GK2DonkeyAPI.Catalog.SpeedOf(d, MountStyle.Cart);
            SpeedWord = GK2DonkeyAPI.Catalog.SpeedWord(Speed, MountStyle.Ride);
            Cargo = d.Cart ? GK2DonkeyAPI.Catalog.CargoOf(d) : 0;
            Effect = d.Effect ?? "";
            EffectWhen = d.EffectWhen;
            Noun = (d.Noun ?? d.Label).ToLowerInvariant();
            Abilities = d.Abilities.Length;
            Description = d.Description ?? "";
            HasPortrait = d.PortraitTexture != null || !string.IsNullOrEmpty(d.Portrait);
            var stats = new List<MountStat>();
            if (d.Stats != null) foreach (var st in d.Stats) stats.Add(new MountStat { Label = st.Key, Value = st.Value });
            Stats = stats;
            var abilities = new List<MountAbilityInfo>();
            foreach (MountAbility a in d.Abilities) abilities.Add(new MountAbilityInfo(a));
            AbilityInfos = abilities;
            Tuning = CopyOf(d.Tuning);
            TuningRide = CopyOf(d.TuningRide);
            TuningCart = CopyOf(d.TuningCart);
            Sounds = CopyOf(d.Sounds);
            SoundsRide = CopyOf(d.SoundsRide);
            SoundsCart = CopyOf(d.SoundsCart);
            SoundsBaby = CopyOf(d.SoundsBaby);
            // the Default skin first (the skin whose id is the mount's; the donkey's own art for the donkey), then the rest
            var skins = new List<string>();
            string first = GK2DonkeyAPI.Looks.DefaultSkinOf(d);
            bool hasDefault = d == GK2DonkeyAPI.Catalog.Donkey;
            foreach (GK2DonkeyAPI.Looks.SkinDef s in GK2DonkeyAPI.Catalog.SkinsOf(d)) if (s.Id == first) hasDefault = true;
            if (hasDefault) skins.Add(first);
            foreach (GK2DonkeyAPI.Looks.SkinDef s in GK2DonkeyAPI.Catalog.SkinsOf(d)) if (s.Id != first) skins.Add(s.Id);
            Skins = skins;
        }

        private static IReadOnlyDictionary<string, object> CopyOf(Dictionary<string, object> d)
        {
            return d == null || d.Count == 0 ? null : new Dictionary<string, object>(d, StringComparer.OrdinalIgnoreCase);
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
        // Takes off and flies / jumps, ridden plain (MountDef.Flies, Jumps; [Mount] flies, jumps).
        public bool Flies { get; }
        public bool Jumps { get; }
        // A baby's size as a fraction of the grown one.
        public float FoalScale { get; }
        // Its speeds, times the keeper's walking speed (its own, else the donkey's; with the cart its own times the
        // donkey's cart-to-riding ratio), the panel's word for the riding one ("Very slow", "Slow", "Normal", "Fast",
        // "Very fast" against the donkey's), and the items its cart carries (0 for a mount that can't pull one).
        public float Speed { get; }
        public float CartSpeed { get; }
        public string SpeedWord { get; }
        public int Cargo { get; }
        public string Effect { get; }
        public MountEffectWhen EffectWhen { get; }
        // The word the game's sentences use for one ("skull", "giant rat").
        public string Noun { get; }
        public int Abilities { get; }
        // For the Mounts panel: the description, whether a portrait exists, the stat lines, the abilities by name.
        public string Description { get; }
        public bool HasPortrait { get; }
        public IReadOnlyList<MountStat> Stats { get; }
        public IReadOnlyList<MountAbilityInfo> AbilityInfos { get; }
        // Its values for existing tuning keys (creature facts: speed, sounds), both styles and per style; null when none.
        public IReadOnlyDictionary<string, object> Tuning { get; }
        public IReadOnlyDictionary<string, object> TuningRide { get; }
        public IReadOnlyDictionary<string, object> TuningCart { get; }
        // Its sounds by event key: a pack's lines as text ("voice" -> "roar_1.ogg, roar_2.ogg"), a code mount's MountSound
        // objects; both styles and per style; null when none (docs/mount-sounds-design.md).
        public IReadOnlyDictionary<string, object> Sounds { get; }
        public IReadOnlyDictionary<string, object> SoundsRide { get; }
        public IReadOnlyDictionary<string, object> SoundsCart { get; }
        public IReadOnlyDictionary<string, object> SoundsBaby { get; }
        // The ids of its skins, its Default first ("donkey", the game's own art, for the donkey mount).
        public IReadOnlyList<string> Skins { get; }

        public override string ToString() => Name + " (" + Id + ")";
    }
}
