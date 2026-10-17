using System;
using System.Collections.Generic;
using GK2DonkeyAPI.Api;

namespace GK2DonkeyAPI
{
    // Mount kinds (docs/mounts-design.md): what the player rides as a creature type. A kind owns the rider's seat for
    // it (values for existing tuning keys, per style), its rules (the cart, foals), a default effect and its abilities
    // (code hooks, MountAbility). A skin names its kind; an instance's kind is its skin's, so nothing new is saved.
    // The donkey kind is the base the game gives us; the built-in creatures declare theirs in their skin.cfg [Mount]
    // sections (the pack path, exercised by the shipped skins every start); mods register kinds with abilities in code.
    internal static class Kinds
    {
        internal const string DonkeyId = MountLooks.DonkeyId;

        internal sealed class KindDef
        {
            internal string Id, Name, Author, Version, Owner;
            internal bool BuiltIn, FromPack;
            internal bool Cart = true;                 // may pull the cart
            internal bool Foals = true;                // foals may be drawn as it
            internal string Effect;                    // the effect an instance shows when its own key says "the mount's"
            internal MountEffectWhen EffectWhen = MountEffectWhen.Always;
            // values for existing tuning keys (a pack's as cfg text, code's typed): both styles, riding, with the cart
            internal Dictionary<string, object> Tuning, TuningRide, TuningCart;
            internal MountAbility[] Abilities = Array.Empty<MountAbility>();

            internal string Label => string.IsNullOrEmpty(Name) ? Id : Name;
        }

        private static readonly Dictionary<string, KindDef> defs = new Dictionary<string, KindDef>(StringComparer.OrdinalIgnoreCase);
        internal static IEnumerable<KindDef> All => defs.Values;
        internal static KindDef Donkey { get; private set; }

        // Bumped at every change: the rigs and the skins re-resolve their kind when it moves (one int compare a tick).
        internal static int Version { get; private set; } = 1;

        internal static void Bind()
        {
            Donkey = new KindDef { Id = DonkeyId, Name = "Donkey", Author = "the game", Version = "1.0", Owner = Plugin.Guid, BuiltIn = true };
            defs[DonkeyId] = Donkey;
        }

        internal static KindDef Find(string id) => !string.IsNullOrEmpty(id) && defs.TryGetValue(id.Trim(), out KindDef d) ? d : null;

        private static void CheckId(string id)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("A mount kind needs an id.");
            foreach (char c in id)
            {
                if (!(char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_')) throw new ArgumentException($"Mount kind id '{id}': letters, digits, dots, dashes and underscores only.");
            }
        }

        // Code: the same owner replaces (its instances re-apply), another owner is refused; the donkey kind is the API's.
        internal static void Register(KindDef d)
        {
            CheckId(d.Id);
            if (d.Abilities == null) d.Abilities = Array.Empty<MountAbility>();
            if (defs.TryGetValue(d.Id, out KindDef old) && old.Owner != d.Owner) throw new ArgumentException($"Mount kind '{d.Id}' is already registered by {old.Owner}.");
            if (d.Id.Equals(DonkeyId, StringComparison.OrdinalIgnoreCase) && d.Owner != Plugin.Guid) throw new ArgumentException("'donkey' is the game's own mount, not a kind to register.");
            defs[d.Id] = d;
            Changed();
            Plugin.Log.LogInfo($"Kinds: mount '{d.Id}' ({d.Label}) registered by {d.Owner}{(d.BuiltIn ? ", built in" : "")}: cart {(d.Cart ? "yes" : "no")}, foals {(d.Foals ? "yes" : "no")}, {d.Abilities.Length} abilit{(d.Abilities.Length == 1 ? "y" : "ies")}{(d.Effect != null ? ", effect " + d.Effect : "")}.");
        }

        // A pack's [Mount] declaration: the first declaration of an id wins, a later one joins it (reported once).
        internal static KindDef Declare(KindDef d, string where)
        {
            CheckId(d.Id);
            if (defs.TryGetValue(d.Id, out KindDef have))
            {
                if (have.FromPack && have.Owner == d.Owner && Same(have, d)) return have;
                Plugin.Log.LogInfo($"Kinds: {where} declares mount '{d.Id}', which {(have.FromPack ? "another pack" : have.Owner)} already defines; the skin joins that one.");
                return have;
            }
            d.FromPack = true;
            d.Abilities = Array.Empty<MountAbility>();
            defs[d.Id] = d;
            Changed();
            Plugin.Log.LogInfo($"Kinds: mount '{d.Id}' ({d.Label}) declared by {where}: cart {(d.Cart ? "yes" : "no")}, foals {(d.Foals ? "yes" : "no")}{(d.Effect != null ? ", effect " + d.Effect : "")}{(d.Tuning != null || d.TuningRide != null || d.TuningCart != null ? ", with tuning" : "")}.");
            return d;
        }

        private static bool Same(KindDef a, KindDef b)
        {
            return a.Cart == b.Cart && a.Foals == b.Foals && a.Effect == b.Effect && a.EffectWhen == b.EffectWhen && a.Name == b.Name
                && SameDict(a.Tuning, b.Tuning) && SameDict(a.TuningRide, b.TuningRide) && SameDict(a.TuningCart, b.TuningCart);
        }

        private static bool SameDict(Dictionary<string, object> a, Dictionary<string, object> b)
        {
            if ((a == null || a.Count == 0) && (b == null || b.Count == 0)) return true;
            if (a == null || b == null || a.Count != b.Count) return false;
            foreach (var kv in a)
            {
                if (!b.TryGetValue(kv.Key, out object v) || !Equals(v, kv.Value)) return false;
            }
            return true;
        }

        internal static bool Unregister(string id, string owner)
        {
            KindDef d = Find(id);
            if (d == null || d.Owner != owner || d == Donkey) return false;
            defs.Remove(d.Id);
            Changed();
            Plugin.Log.LogInfo($"Kinds: mount '{d.Id}' withdrawn by {owner}; its skins fall back to the donkey kind.");
            return true;
        }

        private static void Changed()
        {
            Version++;
            Mount.ForEachInstance(i => i.LooksRig.MarkDirty(), bind: false);
            Mounts.Kinds.RaiseChanged();
        }

        // A kind's or a skin's values changed in place (the tuning panel): the instances of that kind (every instance
        // when none is given) re-merge their tuning now.
        internal static void Touch(KindDef kind = null)
        {
            Version++;
            Mount.ForEachInstance(i =>
            {
                if (kind == null || i.LooksRig.KindNow == kind) i.LooksRig.RetuneNow();
            }, bind: false);
        }

        // The panel or a reload: the kind's values replaced in place (the same object stays referenced), from a
        // declaration of the same owner.
        internal static void Redeclare(KindDef into, KindDef from)
        {
            into.Name = from.Name ?? into.Name;
            into.Cart = from.Cart;
            into.Foals = from.Foals;
            into.Effect = from.Effect;
            into.EffectWhen = from.EffectWhen;
            into.Tuning = from.Tuning;
            into.TuningRide = from.TuningRide;
            into.TuningCart = from.TuningCart;
            Touch(into);
        }

        // The skins of a kind, for the API's info and the settings labels.
        internal static List<Looks.SkinDef> SkinsOf(KindDef k)
        {
            var list = new List<Looks.SkinDef>();
            foreach (Looks.SkinDef s in Looks.All)
            {
                if (Looks.KindOf(s) == k) list.Add(s);
            }
            return list;
        }
    }
}
