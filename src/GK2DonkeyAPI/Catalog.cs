using System;
using System.Collections.Generic;
using GK2DonkeyAPI.Api;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // Mounts (docs/mounts-design.md): what the player rides as a creature type. A mount owns the rider's seat for
    // it (values for existing tuning keys, per style), its rules (the cart, foals), a default effect and its abilities
    // (code hooks, MountAbility). A skin names its mount; an instance's mount is its skin's, so nothing new is saved.
    // The donkey mount is the base the game gives us; the built-in creatures declare theirs in their skin.cfg [Mount]
    // sections (the pack path, exercised by the shipped skins every start); mods register mounts with abilities in code.
    internal static class Catalog
    {
        internal const string DonkeyId = MountLooks.DonkeyId;

        internal sealed class Entry
        {
            internal string Id, Name, Author, Version, Owner;
            internal string Noun;                      // the word a sentence uses ("a skull can't pull the cart"); null: the name in lower case
            internal string Description;               // for the Mounts panel: a line or two about the creature
            internal string Portrait;                  // a pack's: a PNG of the declaring skin's folder for the panel's list; null: the mount drawn
            internal Texture2D PortraitTexture;        // a code mount's portrait, the mod's own texture
            internal List<KeyValuePair<string, string>> Stats = new List<KeyValuePair<string, string>>(); // [Mount.Stats]: lines the panel shows as given
            internal string NounText => (Noun ?? Label).ToLowerInvariant(); // the word every sentence and label uses
            internal bool BuiltIn, FromPack;
            internal bool Cart = true;                 // may pull the cart
            internal bool Foals = true;                // foals may be drawn as it
            internal float FoalScale = 0.6f;           // a baby's size as a fraction of the grown one (the view's scale)
            internal float? Speed, CartSpeed;          // times the keeper's walking speed, riding / with the cart ([Mount] speed, cart_speed); null: the donkey's
            internal string Effect;                    // the effect an instance shows when its own key says "the mount's"
            internal MountEffectWhen EffectWhen = MountEffectWhen.Always;
            // values for existing tuning keys (a pack's as cfg text, code's typed): both styles, riding, with the cart
            internal Dictionary<string, object> Tuning, TuningRide, TuningCart;
            internal MountAbility[] Abilities = Array.Empty<MountAbility>();
            internal string AbilityOwner;               // the owner the abilities' values are set under ("<owner>.ability.<id>"); cleared when the mount leaves an instance
            internal string DeclaredBy;                 // a pack mount: the skin whose [Mount] block defined it (its Default when the ids match)
            internal bool DeclaredByDefault => FromPack && DeclaredBy != null && DeclaredBy.Equals(Id, StringComparison.OrdinalIgnoreCase);

            internal string Label => string.IsNullOrEmpty(Name) ? Id : Name;
        }

        private static readonly Dictionary<string, Entry> defs = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        internal static IEnumerable<Entry> All => defs.Values;
        internal static Entry Donkey { get; private set; }

        // Bumped at every change: the rigs and the skins re-resolve their mount when it moves (one int compare a tick).
        internal static int Version { get; private set; } = 1;

        internal static void Bind()
        {
            Donkey = new Entry { Id = DonkeyId, Name = "Donkey", Author = "the game", Version = "1.0", Owner = Plugin.Guid, BuiltIn = true };
            defs[DonkeyId] = Donkey;
        }

        // A mount's speed (the user, 2026-10-22): times the keeper's walking speed, 1 = walking (the game's speed is
        // linear in PlayerPhysicalBody.SpeedMultiplier: PROBE-FINDINGS.md "Speed is linear"). Its own ([Mount] speed /
        // cart_speed, MountDef.Speed / CartSpeed), else a SpeedMultiplier in its tuning, else the donkey's: the API cfg's
        // [Ride] / [Cart] SpeedMultiplier (2 and 1.5 as shipped), so a default player's donkey is as it always was.
        internal const float SpeedMin = 0.5f, SpeedMax = 4f;
        internal const float DonkeyRideSpeed = 2f, DonkeyCartSpeed = 1.5f; // as shipped: what the panel's words compare with

        internal static float SpeedOf(Entry k, MountStyle style)
        {
            float? own = k == null ? null : style == MountStyle.Cart ? k.CartSpeed : k.Speed;
            if (own.HasValue) return own.Value;
            if (k != null)
            {
                object pin = null;
                if (k.Tuning != null) k.Tuning.TryGetValue("SpeedMultiplier", out pin);
                Dictionary<string, object> styled = style == MountStyle.Cart ? k.TuningCart : k.TuningRide;
                if (styled != null && styled.TryGetValue("SpeedMultiplier", out object p)) pin = p;
                float? f = AsFloat(pin);
                if (f.HasValue) return f.Value;
            }
            StyleSettings s = Plugin.For(style);
            return s?.SpeedMultiplier?.Entry != null ? s.SpeedMultiplier.Entry.Value : style == MountStyle.Cart ? DonkeyCartSpeed : DonkeyRideSpeed;
        }

        // The word the Mounts panel shows for a speed, against the donkey's for that style (so the donkey reads Normal
        // both ways, and a cart's word compares carts): under 0.7 of it Very slow, under 0.9 Slow, to 1.1 Normal, to
        // 1.35 Fast, above Very fast.
        internal static string SpeedWord(float speed, MountStyle style)
        {
            float r = speed / (style == MountStyle.Cart ? DonkeyCartSpeed : DonkeyRideSpeed);
            const float e = 0.001f;
            return r < 0.7f - e ? "Very slow" : r < 0.9f - e ? "Slow" : r <= 1.1f + e ? "Normal" : r <= 1.35f + e ? "Fast" : "Very fast";
        }

        // The stat lines the API writes itself: a [Mount.Stats] line (or a code mount's MountStat) by these names is
        // left out, so the panel never shows two speeds.
        internal static bool ReservedStat(string label)
        {
            string l = (label ?? "").Trim();
            return l.Equals("Speed", StringComparison.OrdinalIgnoreCase) || l.Equals("Cart speed", StringComparison.OrdinalIgnoreCase);
        }

        internal static float? AsFloat(object v)
        {
            if (v is string s && float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float f)) return f;
            try
            {
                if (v is IConvertible && !(v is string)) return Convert.ToSingle(v, System.Globalization.CultureInfo.InvariantCulture); // a code mount's int or double
            }
            catch
            {
            }
            return null;
        }

        internal static Entry Find(string id) => !string.IsNullOrEmpty(id) && defs.TryGetValue(id.Trim(), out Entry d) ? d : null;

        private static void CheckId(string id)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("A mount needs an id.");
            foreach (char c in id)
            {
                if (!(char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_')) throw new ArgumentException($"Mount id '{id}': letters, digits, dots, dashes and underscores only.");
            }
        }

        // Code: the same owner replaces (its instances re-apply), another owner is refused; the donkey mount is the API's.
        internal static void Register(Entry d)
        {
            CheckId(d.Id);
            if (d.Abilities == null) d.Abilities = Array.Empty<MountAbility>();
            if (defs.TryGetValue(d.Id, out Entry old) && old.Owner != d.Owner) throw new ArgumentException($"Mount '{d.Id}' is already registered by {old.Owner}.");
            if (d.Id.Equals(DonkeyId, StringComparison.OrdinalIgnoreCase) && d.Owner != Plugin.Guid) throw new ArgumentException("'donkey' is the game's own mount, not a mount to register.");
            defs[d.Id] = d;
            Changed();
            Plugin.Log.LogInfo($"Catalog: mount '{d.Id}' ({d.Label}) registered by {d.Owner}{(d.BuiltIn ? ", built in" : "")}: cart {(d.Cart ? "yes" : "no")}, foals {(d.Foals ? "yes" : "no")}, {d.Abilities.Length} abilit{(d.Abilities.Length == 1 ? "y" : "ies")}{(d.Effect != null ? ", effect " + d.Effect : "")}.");
        }

        // A pack's [Mount] declaration. The mount's facts belong to its Default skin (the skin whose id is the mount's,
        // docs/mounts-foundation-2.md 2.2): the first declaration defines the mount, and a Default that comes later
        // re-declares it over a joiner's (a loose pack declaring 'dragon' before Saddle Up's dragon registers); any
        // other later declaration joins, its own facts and tuning unused (said once). A code-registered mount is never
        // re-declared by a pack.
        internal static Entry Declare(Entry d, string where, string skinId = null)
        {
            CheckId(d.Id);
            if (defs.TryGetValue(d.Id, out Entry have))
            {
                if (have.FromPack && have.Owner == d.Owner && Same(have, d))
                {
                    if (have.DeclaredBy == null && skinId != null) have.DeclaredBy = skinId;
                    return have;
                }
                bool isDefault = skinId != null && skinId.Equals(d.Id, StringComparison.OrdinalIgnoreCase);
                // the mount's own skin re-declares over a joiner's declaration, and over the declaration of an earlier
                // copy of itself that the version rule replaced (Looks.Register: the winner's facts, not the loser's)
                if (have.FromPack && isDefault && (!have.DeclaredByDefault || have.Owner != d.Owner))
                {
                    Plugin.Log.LogInfo($"Catalog: {where} is mount '{d.Id}''s own skin: its [Mount] block defines the mount from now on, over {(have.DeclaredBy != null ? "skin '" + have.DeclaredBy + "'" : have.Owner)}'s declaration (which joins).");
                    have.Owner = d.Owner;
                    have.BuiltIn = d.BuiltIn;
                    have.DeclaredBy = skinId;
                    Redeclare(have, d);
                    return have;
                }
                bool differs = !Same(have, d);
                Plugin.Log.LogInfo($"Catalog: {where} declares mount '{d.Id}', which {(have.FromPack ? (have.Owner == d.Owner ? "another skin" : "another pack") : have.Owner)} already defines; the skin joins that one{(differs ? ", and its own [Mount] facts and tuning are not used (the mount's own skin's are)" : "")}.");
                return have;
            }
            d.FromPack = true; // BuiltIn is the declaring skin's (set by Looks.Register): a built-in skin's mount is built in
            d.Abilities = Array.Empty<MountAbility>();
            d.DeclaredBy = skinId;
            defs[d.Id] = d;
            Changed();
            Plugin.Log.LogInfo($"Catalog: mount '{d.Id}' ({d.Label}) declared by {where}: cart {(d.Cart ? "yes" : "no")}, foals {(d.Foals ? "yes, " + d.FoalScale.ToString("0.00") + "x" : "no")}{(d.Speed.HasValue ? ", speed " + d.Speed.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) : "")}{(d.CartSpeed.HasValue ? ", cart speed " + d.CartSpeed.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) : "")}{(d.Effect != null ? ", effect " + d.Effect : "")}{(d.Tuning != null || d.TuningRide != null || d.TuningCart != null ? ", with tuning" : "")}.");
            return d;
        }

        private static bool Same(Entry a, Entry b)
        {
            return a.Cart == b.Cart && a.Foals == b.Foals && Math.Abs(a.FoalScale - b.FoalScale) < 0.001f && a.Speed == b.Speed && a.CartSpeed == b.CartSpeed && a.Effect == b.Effect && a.EffectWhen == b.EffectWhen && a.Name == b.Name
                && a.Author == b.Author && a.Version == b.Version && a.Noun == b.Noun && a.Description == b.Description && a.Portrait == b.Portrait && SameStats(a.Stats, b.Stats)
                && SameDict(a.Tuning, b.Tuning) && SameDict(a.TuningRide, b.TuningRide) && SameDict(a.TuningCart, b.TuningCart);
        }

        private static bool SameStats(List<KeyValuePair<string, string>> a, List<KeyValuePair<string, string>> b)
        {
            int na = a?.Count ?? 0, nb = b?.Count ?? 0;
            if (na != nb) return false;
            for (int i = 0; i < na; i++) if (a[i].Key != b[i].Key || a[i].Value != b[i].Value) return false;
            return true;
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
            Entry d = Find(id);
            if (d == null || d.Owner != owner || d == Donkey) return false;
            defs.Remove(d.Id);
            Changed();
            Plugin.Log.LogInfo($"Catalog: mount '{d.Id}' withdrawn by {owner}; its skins fall back to the donkey mount.");
            return true;
        }

        private static void Changed()
        {
            Version++;
            Looks.MarkWearersDirty();
            Mounts.Catalog.RaiseChanged();
        }

        // A mount's or a skin's values changed in place (the tuning panel): the instances of that mount (every instance
        // when none is given) re-merge their tuning now.
        internal static void Touch(Entry mount = null)
        {
            Version++;
            Mount.ForEachInstance(i =>
            {
                if (mount == null || i.LooksRig.MountNow == mount) i.LooksRig.RetuneNow();
            }, bind: false);
        }

        // The panel or a reload: the mount's values replaced in place (the same object stays referenced), from a
        // declaration of the same owner.
        internal static void Redeclare(Entry into, Entry from)
        {
            into.Name = from.Name ?? into.Name;
            into.Noun = from.Noun;
            into.Description = from.Description;
            into.Portrait = from.Portrait;
            into.Stats = from.Stats ?? new List<KeyValuePair<string, string>>();
            into.Author = from.Author;
            into.Version = from.Version;
            into.Cart = from.Cart;
            into.Foals = from.Foals;
            into.FoalScale = from.FoalScale;
            into.Speed = from.Speed;
            into.CartSpeed = from.CartSpeed;
            into.Effect = from.Effect;
            into.EffectWhen = from.EffectWhen;
            into.Tuning = from.Tuning;
            into.TuningRide = from.TuningRide;
            into.TuningCart = from.TuningCart;
            Touch(into);
        }

        // The skins of a mount, for the API's info and the settings labels.
        internal static List<Looks.SkinDef> SkinsOf(Entry k)
        {
            var list = new List<Looks.SkinDef>();
            foreach (Looks.SkinDef s in Looks.All)
            {
                if (Looks.MountOf(s) == k) list.Add(s);
            }
            return list;
        }
    }
}
