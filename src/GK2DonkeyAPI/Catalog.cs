using System;
using System.Collections.Generic;
using GK2DonkeyAPI.Api;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // Mounts (docs/mounts-design.md): what the player rides as a creature type. A mount owns the rider's seat for
    // it (values for existing tuning keys, per style), its rules (the cart, foals), a default effect and its abilities
    // (code hooks, MountAbility). A skin names its mount; an instance's mount is its skin's, so nothing new is saved.
    // The donkey mount is the base the game gives us; the built-in creatures come as mount packs (a folder per mount:
    // mount.cfg, its sounds and its skins; MountPacks.cs, exercised by Saddle Up's every start); mods register mounts
    // with abilities in code.
    internal static class Catalog
    {
        internal const string DonkeyId = MountLooks.DonkeyId;

        internal sealed class Entry
        {
            internal string Id, Name, Author, Version, Owner;
            internal string Needs;     // a pack's [Mount] needs = (the API version it was written for), kept for its Save
            internal int Format;       // a pack's [Mount] format = (0: not given)
            internal string Noun;                      // the word a sentence uses ("a skull can't pull the cart"); null: the name in lower case
            internal string Description;               // for the Mounts panel: a line or two about the creature
            internal string Portrait;                  // a pack's: a PNG of the mount's folder for the panel's list; null: the mount drawn
            internal Texture2D PortraitTexture;        // a code mount's portrait, the mod's own texture
            internal List<KeyValuePair<string, string>> Stats = new List<KeyValuePair<string, string>>(); // mount.cfg [Stats]: lines the panel shows as given
            internal string NounText => (Noun ?? Label).ToLowerInvariant(); // the word every sentence and label uses
            internal bool BuiltIn, FromPack;
            internal bool Cart = true;                 // may pull the cart
            internal bool Foals = true;                // foals may be drawn as it
            internal bool Flies;                       // takes off and flies ([Mount] flies; the flying experiment, docs/flying.md)
            internal bool Jumps = true;                // jumps, riding plain ([Mount] jumps = false: never)
            internal float FoalScale = 0.6f;           // a baby's size as a fraction of the grown one (the view's scale)
            internal float? Speed;                     // riding, times the keeper's walking speed ([Mount] speed); null: the donkey's. With the cart: times CartFactor
            internal int? Cargo;                       // items its cart carries, 1 to CargoMax ([Mount] cargo); null: CargoMax. Shown only, for now
            // values for existing tuning keys (a pack's as cfg text, code's typed): both styles, riding, with the cart
            internal Dictionary<string, object> Tuning, TuningRide, TuningCart;
            // its own jump (mount.cfg [Jump], MountDef.Jump; JumpShapes.cs): the keys it sets, the rest the default jump's; null: the default
            internal Dictionary<string, float> Jump;
            // the mount's sounds (mount.cfg [Sounds], [Sounds.Ride], [Sounds.Cart], [Sounds.Baby]: a pack's text lines; a code
            // mount's MountSound objects), parsed in LooksSounds.cs; docs/mount-sounds-design.md
            internal Dictionary<string, object> Sounds, SoundsRide, SoundsCart, SoundsBaby;
            internal Looks.SoundBlocks SoundBlocks;
            internal MountAbility[] Abilities = Array.Empty<MountAbility>();
            internal string AbilityOwner;               // the owner the abilities' values are set under ("<owner>.ability.<id>"); cleared when the mount leaves an instance
            internal Looks.MountPack Pack;               // a pack mount: its folder (mount.cfg, sounds\, the portrait); null: a code mount or the donkey

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
        // linear in PlayerPhysicalBody.SpeedMultiplier: PROBE-FINDINGS.md "Speed is linear"). Its own ([Mount] speed,
        // MountDef.Speed), else a SpeedMultiplier in its tuning, else the donkey's: the API cfg's [Ride] / [Cart]
        // SpeedMultiplier (2 and 1.5 as shipped), so a default player's donkey is as it always was. With the cart a mount
        // that says its speed goes at it times CartFactor, the donkey's cart speed over its riding one (0.75 as shipped):
        // the cart slows every mount the same way (the user: "always relative to the speed of the mount"; carts of their
        // own speed maybe later).
        internal const float SpeedMin = 0.5f, SpeedMax = 4f;
        internal const float DonkeyRideSpeed = 2f, DonkeyCartSpeed = 1.5f; // as shipped: what the panel's words compare with
        // The cart's most items (the CartSlots row's top, 1 to 6): a mount's cargo stat is 1 to this.
        internal const int CargoMax = 6;

        internal static int CargoOf(Entry k) => k?.Cargo ?? CargoMax;

        internal static float CartFactor
        {
            get
            {
                float ride = Plugin.For(MountStyle.Ride)?.SpeedMultiplier?.Entry?.Value ?? DonkeyRideSpeed;
                float cart = Plugin.For(MountStyle.Cart)?.SpeedMultiplier?.Entry?.Value ?? DonkeyCartSpeed;
                return ride > 0.01f ? cart / ride : DonkeyCartSpeed / DonkeyRideSpeed;
            }
        }

        // The speed a mount's own value gives in a style: riding as said, with the cart times CartFactor; null without one.
        internal static float? OwnSpeed(Entry k, MountStyle style) => k?.Speed == null ? (float?)null : style == MountStyle.Cart ? k.Speed.Value * CartFactor : k.Speed.Value;

        internal static float SpeedOf(Entry k, MountStyle style)
        {
            float? own = OwnSpeed(k, style);
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

        // The stat lines the API writes itself: a mount.cfg [Stats] line (or a code mount's MountStat) by these names is
        // left out, so the panel never shows two speeds.
        internal static bool ReservedStat(string label)
        {
            string l = (label ?? "").Trim();
            return l.Equals("Speed", StringComparison.OrdinalIgnoreCase) || l.Equals("Cargo", StringComparison.OrdinalIgnoreCase);
        }

        internal static float? AsFloat(object v)
        {
            if (v is string s) return Numbers.TryFloat(s, out float f) ? f : (float?)null;
            try
            {
                if (v is IConvertible && !(v is string))
                {
                    float c = Convert.ToSingle(v, System.Globalization.CultureInfo.InvariantCulture); // a code mount's int or double
                    return Numbers.IsFinite(c) ? c : (float?)null;
                }
            }
            catch
            {
            }
            return null;
        }

        internal static Entry Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            string t = id.Trim();
            if (defs.TryGetValue(t, out Entry d)) return d;
            return renamedMounts.TryGetValue(t, out string now) && defs.TryGetValue(now, out d) ? d : null;
        }

        // Saddle Up's four mounts before they became packs of their own, their ids by the pack (2026-10-26, packs/; the
        // user: "ids need to be depending on pack"). Never released: what a dev's own world, cfg and side file held. The
        // old id finds the new mount (Find, Looks.Find's "<mount>/<skin>"), the Mount row writes the new one back, and a
        // per-mount cfg key of the old id carries its value over (MountSettingsPage.OldPlaces, RenamedMountKey).
        internal static readonly Dictionary<string, string> renamedMounts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["saddleup.dragon"] = "saddleup-dragon.dragon",
            ["saddleup.rat"] = "saddleup-critters.rat",
            ["saddleup.snail"] = "saddleup-critters.snail",
            ["saddleup.worm"] = "saddleup-critters.worm",
        };

        // A mount by the name after its prefix ("dragon" -> saddleup.dragon) when exactly one mount has it: what a dev's
        // own cfg and saves held before the prefixes (2026-10-24). Null when none or several.
        internal static Entry FindShort(string name)
        {
            if (string.IsNullOrEmpty(name) || name.IndexOf('.') >= 0) return null;
            Entry only = null;
            foreach (Entry e in defs.Values)
            {
                if (!e.Id.EndsWith("." + name, StringComparison.OrdinalIgnoreCase)) continue;
                if (only != null) return null;
                only = e;
            }
            return only;
        }

        private static void CheckId(string id)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("A mount needs an id.");
            string why = IdRule(id, "Mount id");
            if (why != null) throw new ArgumentException(why);
            // a mount's id carries its mod's prefix, "<mod>.<name>" (saddleup.dragon, acme.dragon): two mods' dragons are
            // two mounts, and a saved one never turns into another mod's (docs/mount-id-collisions.md; the user,
            // 2026-10-24). Only the game's own donkey is plain.
            int dot = id.IndexOf('.');
            if (!id.Equals(DonkeyId, StringComparison.OrdinalIgnoreCase) && (dot <= 0 || dot == id.Length - 1)) throw new ArgumentException($"Mount id '{id}': a mount's id is '<mod>.<name>', your mod's prefix and the mount's name (Saddle Up's dragon is saddleup.dragon; docs/making-a-mount.md).");
        }

        // An id's characters (a mount's, a skin's name, an effect's; docs/multi-mod-loading.md 5.6): ASCII letters and
        // digits, dots, dashes and underscores, at most IdMax long, no dot at either end (Windows drops a trailing dot from
        // a folder's name: the sound cache's), no two dots in a row. ASCII only: "saddleup.drаgon" with a Cyrillic "а"
        // looked like the dragon and was a second mount. Null when it is fine, else why not.
        internal const int IdMax = 64;

        internal static string IdRule(string id, string what)
        {
            if (string.IsNullOrEmpty(id)) return $"{what}: empty.";
            if (id.Length > IdMax) return $"{what} '{id.Substring(0, 24)}...': at most {IdMax} characters.";
            foreach (char c in id)
            {
                if (!(c < 128 && (char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_'))) return $"{what} '{id}': letters (a-z, A-Z), digits, dots, dashes and underscores only.";
            }
            if (id[0] == '.' || id[id.Length - 1] == '.' || id.Contains("..")) return $"{what} '{id}': no dot at the start or the end, nor two in a row.";
            return null;
        }

        // Code: the same owner replaces (its instances re-apply); another owner's mount of the same id (code or pack)
        // meets the one version rule (Outranks; the user, 2026-10-24: "one version rule, no throw"): the newer wins, the
        // loser is left out with one warning and false (its handle then does nothing). A mistake in the call itself (no
        // id, no prefix, the donkey's id) still throws. The donkey mount is the API's.
        internal static bool Register(Entry d)
        {
            CheckId(d.Id);
            if (d.Abilities == null) d.Abilities = Array.Empty<MountAbility>();
            if (d.Id.Equals(DonkeyId, StringComparison.OrdinalIgnoreCase) && d.Owner != Plugin.Guid) throw new ArgumentException("'donkey' is the game's own mount, not a mount to register.");
            d.FromPack = false;
            if (defs.TryGetValue(d.Id, out Entry old) && old.Owner != d.Owner)
            {
                if (!Outranks(d, old))
                {
                    Plugin.Log.LogWarning($"Catalog: mount '{d.Id}' from {Where(d)} (version {d.Version ?? "none"}) is left out: {Where(old)} has it (version {old.Version ?? "none"}) and this one is not newer. Remove the one you don't want, or raise the version of the one you do.");
                    NoteLost(d);
                    return false;
                }
                Plugin.Log.LogWarning($"Catalog: mount '{d.Id}' from {Where(d)} (version {d.Version ?? "none"}) replaces the one from {Where(old)} (version {old.Version ?? "none"}): the newer wins. Remove the old one.");
                Retire(old);
                NoteLost(old);
            }
            lost.RemoveAll(l => l.Id.Equals(d.Id, StringComparison.OrdinalIgnoreCase) && l.Owner == d.Owner);
            defs[d.Id] = d;
            Changed();
            Looks.MountArrived(d);
            Late(d.Owner, "the mount '" + d.Id + "'");
            Plugin.Log.LogInfo($"Catalog: mount '{d.Id}' ({d.Label}) registered by {d.Owner}{(d.BuiltIn ? ", built in" : "")}: cart {(d.Cart ? "yes" : "no")}, foals {(d.Foals ? "yes" : "no")}{(d.Flies ? ", flies" : "")}{(d.Jumps ? "" : ", doesn't jump")}, {d.Abilities.Length} abilit{(d.Abilities.Length == 1 ? "y" : "ies")}.");
            return true;
        }

        // The mounts that lost their id to another owner's this session (the version rule), by id and owner: their own
        // looks are left out with them (Looks.Register), never put on the winner. Back at the next start.
        private static readonly List<Entry> lost = new List<Entry>();

        private static void NoteLost(Entry k)
        {
            if (!Lost(k.Id, k.Owner)) lost.Add(k);
        }

        private static void NoteLost(string id, string owner) => NoteLost(new Entry { Id = id, Owner = owner });

        // The window for registering (the user, 2026-10-26: Awake or Start): the settings page reads every list the frame
        // after its Publish (MountSettingsPage.Show). A mount or skin that comes after a page was shown works in the world
        // but has no Skin or colour rows this session: said once per mod, so its author knows.
        private static readonly HashSet<string> lateLogged = new HashSet<string>(StringComparer.Ordinal);

        internal static void Late(string owner, string what)
        {
            if (!Api.MountSettingsPage.AnyShown || owner == null || owner.StartsWith(Plugin.Guid, StringComparison.Ordinal)) return; // the API's own: its packs' reloads, the panel, the self-tests
            if (lateLogged.Add(owner)) Plugin.Log.LogInfo($"Catalog: {owner} registered {what} after the settings were shown: it works in the world, but has no Skin or colour rows until the next start. Register mounts and skins in your plugin's Awake or Start (docs/making-a-mount.md).");
        }

        // Several registrations as one change (a pack folder, a DLL's embedded packs): MountsChanged once at the end.
        private static int batch;
        private static bool batchChanged;

        internal static IDisposable Batch()
        {
            batch++;
            return new BatchEnd();
        }

        private sealed class BatchEnd : IDisposable
        {
            private bool done;

            public void Dispose()
            {
                if (done) return;
                done = true;
                if (--batch > 0 || !batchChanged) return;
                batchChanged = false;
                Mounts.Catalog.RaiseChanged();
            }
        }

        internal static bool Lost(string id, string owner)
        {
            if (string.IsNullOrEmpty(id) || owner == null) return false;
            foreach (Entry l in lost) if (l.Owner == owner && l.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        // Where a mount came from, for the log: a loose folder, a mod's DLL, or a mod's code.
        internal static string Where(Entry k) => k.Pack?.Source != null ? "the folder " + k.Pack.Source : k.Pack?.SourceAssembly != null ? k.Pack.SourceAssembly.GetName().Name + ".dll" : "code (" + k.Owner + ")";

        // A mount that lost its id to another owner's: what it had registered beside its entry goes (a pack's sound
        // files, a code mount's clips); its skins re-resolve to the winner (Version).
        private static void Retire(Entry k)
        {
            if (k.Pack != null) SoundBank.ForgetFiles(k.Pack.SoundFiles);
            SoundBank.UnregisterAll(SoundBankOwner(k));
        }

        // The sound bank owner of a code mount's own clips (Sounds.Rig, SoundBank.ForClips): its mod and id, so two
        // mods' mounts of one id never share a bank entry.
        internal static string SoundBankOwner(Entry k) => "mount/" + k.Owner + "/" + k.Id;

        // A mount pack's declaration (its mount.cfg; MountPacks.cs). One pack per mount: the same pack again (a second
        // registration of the same owner with the same facts) is the one already there; another pack of the same id
        // meets the skins' version rule (a higher version wins, a tie goes to the DLL over a loose folder, else the
        // first stays) and the winner's facts replace the loser's in place, its skins keeping the object. A code mount
        // meets the same rule (2026-10-24): a newer pack replaces it, an older or equal one is left out.
        internal static Entry Declare(Entry d, string where)
        {
            CheckId(d.Id);
            if (d.Id.Equals(DonkeyId, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("'donkey' is the game's own mount: its folder holds skins only.");
            d.FromPack = true;
            d.Abilities = Array.Empty<MountAbility>();
            if (defs.TryGetValue(d.Id, out Entry have))
            {
                if (!have.FromPack)
                {
                    if (!Outranks(d, have)) throw new ArgumentException($"mount '{d.Id}' is registered in code by {have.Owner} (version {have.Version ?? "none"}); this pack (version {d.Version ?? "none"}) is not newer, so it is left out. Add skins to it with a mount folder without mount.cfg, or raise the version.");
                    Plugin.Log.LogWarning($"Catalog: mount '{d.Id}' from {where} (version {d.Version ?? "none"}) replaces the one from {Where(have)} (version {have.Version ?? "none"}): the newer wins. Remove the old one.");
                    Retire(have);
                    defs[d.Id] = d;
                    Changed();
                    return d;
                }
                if (have.Owner == d.Owner && Same(have, d)) return have;
                if (have.Owner == d.Owner || Outranks(d, have))
                {
                    string oldOwner = have.Owner;
                    bool newOwner = oldOwner != d.Owner;
                    if (newOwner) Plugin.Log.LogWarning($"Catalog: mount '{d.Id}' from {where} (version {d.Version ?? "none"}) replaces the one of {have.Owner} (version {have.Version ?? "none"}): the newer wins. Delete the old one.");
                    // the old pack's sound files and portrait go: a new pack's are read anew (they stayed in the bank)
                    if (have.Pack != null && have.Pack != d.Pack)
                    {
                        SoundBank.ForgetFiles(have.Pack.SoundFiles);
                        Looks.ForgetPortraits(have.Id + "(mount)");
                    }
                    have.Owner = d.Owner;
                    have.BuiltIn = d.BuiltIn;
                    have.Pack = d.Pack;
                    Redeclare(have, d);
                    if (newOwner)
                    {
                        // the old pack's own skins go with it (the owner is swapped first, so its last skin doesn't take the
                        // mount along): the new pack's skins\default\ registers next, never the old pictures on the new
                        // facts (docs/multi-mod-loading.md 4.1); another mod's skins of the mount stay
                        NoteLost(d.Id, oldOwner);
                        int gone = 0;
                        foreach (Looks.SkinDef sd in SkinsOf(have)) if (sd.Owner == oldOwner && Looks.Unregister(sd.Id, oldOwner)) gone++;
                        if (gone > 0) Plugin.Log.LogInfo($"Catalog: mount '{d.Id}': the {gone} skin(s) of the pack it replaced are withdrawn with it.");
                        Changed();
                    }
                    return have;
                }
                throw new ArgumentException($"mount '{d.Id}' is already defined by {have.Owner} (version {have.Version ?? "none"}); this one (version {d.Version ?? "none"}) is not newer, so it is left out. Delete the one you don't want, or raise the version of the one you do.");
            }
            defs[d.Id] = d;
            Changed();
            Looks.MountArrived(d);
            Late(d.Owner, "the mount '" + d.Id + "'");
            Plugin.Log.LogInfo($"Catalog: mount '{d.Id}' ({d.Label}) declared by {where}: cart {(d.Cart ? "yes" : "no")}, foals {(d.Foals ? "yes, " + d.FoalScale.ToString("0.00") + "x" : "no")}{(d.Flies ? ", flies" : "")}{(d.Jumps ? "" : ", doesn't jump")}{(d.Speed.HasValue ? ", speed " + d.Speed.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) : "")}{(d.Cargo.HasValue ? ", cargo " + d.Cargo.Value : "")}{(d.Tuning != null || d.TuningRide != null || d.TuningCart != null ? ", with tuning" : "")}{(d.Jump != null && d.Jump.Count > 0 ? ", its own jump" : "")}.");
            return d;
        }

        // The one version rule for two mounts of one id, code or pack (as the skins', Looks.Outranks): a higher version
        // wins; at a tie code or a DLL beats a loose folder; else the first stays.
        private static bool Outranks(Entry d, Entry old)
        {
            int cmp = Looks.CompareVersions(d.Version, old.Version);
            if (cmp != 0) return cmp > 0;
            return d.Pack?.Source == null && old.Pack?.Source != null;
        }

        private static bool Same(Entry a, Entry b)
        {
            return a.Cart == b.Cart && a.Foals == b.Foals && a.Flies == b.Flies && a.Jumps == b.Jumps && Math.Abs(a.FoalScale - b.FoalScale) < 0.001f && a.Speed == b.Speed && a.Cargo == b.Cargo && a.Name == b.Name
                && a.Author == b.Author && a.Version == b.Version && a.Noun == b.Noun && a.Description == b.Description && a.Portrait == b.Portrait && SameStats(a.Stats, b.Stats)
                && SameDict(a.Tuning, b.Tuning) && SameDict(a.TuningRide, b.TuningRide) && SameDict(a.TuningCart, b.TuningCart)
                && SameDict(a.Sounds, b.Sounds) && SameDict(a.SoundsRide, b.SoundsRide) && SameDict(a.SoundsCart, b.SoundsCart) && SameDict(a.SoundsBaby, b.SoundsBaby);
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
            SoundBank.UnregisterAll(SoundBankOwner(d)); // a code mount's clips (the entries only: the clips are the mod's)
            Changed();
            Plugin.Log.LogInfo($"Catalog: mount '{d.Id}' withdrawn by {owner}; its skins fall back to the donkey mount.");
            return true;
        }

        private static void Changed()
        {
            Version++;
            Looks.MarkWearersDirty();
            if (batch > 0) batchChanged = true;
            else Mounts.Catalog.RaiseChanged();
        }

        // A mount's or a skin's values changed in place (the tuning panel): the instances of that mount (every instance
        // when none is given) re-merge their tuning now.
        internal static void Touch(Entry mount = null)
        {
            Looks.RefreshInherited(mount?.Id); // a default's values changed: its variants take them (docs/skin-layering-plan.md 2.1)
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
            into.Needs = from.Needs;
            into.Format = from.Format;
            into.Cart = from.Cart;
            into.Foals = from.Foals;
            into.Flies = from.Flies;
            into.Jumps = from.Jumps;
            into.FoalScale = from.FoalScale;
            into.Speed = from.Speed;
            into.Cargo = from.Cargo;
            into.Tuning = from.Tuning;
            into.TuningRide = from.TuningRide;
            into.TuningCart = from.TuningCart;
            into.Jump = from.Jump;
            into.Sounds = from.Sounds;
            into.SoundsRide = from.SoundsRide;
            into.SoundsCart = from.SoundsCart;
            into.SoundsBaby = from.SoundsBaby;
            into.SoundBlocks = null;
            Touch(into);
        }

        // The skins of a mount, for the API's info and the settings labels.
        internal static List<Looks.SkinDef> SkinsOf(Entry k)
        {
            var list = new List<Looks.SkinDef>();
            foreach (Looks.SkinDef s in Looks.All)
            {
                if (Looks.MountOf(s) == k && (k != Donkey || s.MountId.Equals(DonkeyId, StringComparison.OrdinalIgnoreCase))) list.Add(s);
            }
            return list;
        }

        // The skins installed for a mount that isn't (its mod is missing): by the mount's id, for the panel's "not
        // installed" cards. They are no donkey's skins and are offered nowhere until their mount comes.
        internal static Dictionary<string, List<Looks.SkinDef>> WaitingSkins()
        {
            var by = new Dictionary<string, List<Looks.SkinDef>>(StringComparer.OrdinalIgnoreCase);
            foreach (Looks.SkinDef s in Looks.All)
            {
                if (s.MountId.Equals(DonkeyId, StringComparison.OrdinalIgnoreCase) || Find(s.MountId) != null) continue;
                if (!by.TryGetValue(s.MountId, out List<Looks.SkinDef> l)) by[s.MountId] = l = new List<Looks.SkinDef>();
                l.Add(s);
            }
            return by;
        }
    }
}
