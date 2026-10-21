using System;
using System.Collections.Generic;
using System.Reflection;

namespace GK2DonkeyAPI.Api
{
    // How the cart meets a slope (CartFollowsGround): the whole cart lifted level with the shafts at half the lift,
    // the shafts tilted about their harness end, or the cart pitched about its axle like a real cart.
    public enum CartSlopeStyle
    {
        HalfLift,
        ShaftsTilt,
        WholeCartPitch,
    }

    public enum LegSource
    {
        Body,
        Drawn,
    }

    // Tuning value types (MountTuning). Animator states the keeper can be held in while riding (values = the game's AnimationState enum).
    // Only states that loop on their own and carry no control-taking behaviour. Tool, work, planting, cast and pull
    // states either end by forcing Idle (which would re-trigger the pose every frame) or take player control.
    public enum RiderPose
    {
        Walk = 1,
        Idle = 0,
        FishingIdle = 203,
        FocusedWalk = 41,
    }

    // The tuning values of one style (Mounts.Tuning(style)), by the key they have in the [Ride] / [Cart] cfg sections
    // and the panel: "SpeedMultiplier", "LanternLightIntensity", "Pose", "HoofVolume", ...
    //
    // A value set here is a session-only override for your mod: it wins over the player's cfg value, is clamped to the
    // same range the panel allows, is never written to the cfg, and ends with the session, Clear, Reset or
    // Mounts.ClearTuning(yourId). While mounted in this style most values apply at once; values the donkey is built
    // with (cart and lantern geometry measured at summon) apply from the next mount or style switch.
    public sealed class MountTuning
    {
        private readonly Dictionary<string, ITuned> byKey = new Dictionary<string, ITuned>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> keys = new List<string>();

        // The style these values belong to; null for Mounts.Behaviour (General: both styles).
        public MountStyle? Style { get; }

        // The cfg section: "Ride", "Cart" or "General".
        public string Section { get; }

        internal MountTuning(MountStyle style, StyleSettings s)
        {
            Style = style;
            Section = s.Section;
            foreach (FieldInfo f in typeof(StyleSettings).GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (f.GetValue(s) is ITuned t)
                {
                    Add(t);
                }
            }
        }

        internal MountTuning(string section, IEnumerable<ITuned> values)
        {
            Section = section;
            foreach (ITuned t in values)
            {
                Add(t);
            }
        }

        private void Add(ITuned t)
        {
            byKey[t.Key] = t;
            keys.Add(t.Key);
        }

        // Every key, in declaration order.
        public IReadOnlyList<string> Keys => keys;

        public bool Has(string key) => key != null && byKey.ContainsKey(key);

        // The value's type: float, int, bool, string or one of the enums (RiderPose, LegSource, CartSlopeStyle).
        public Type TypeOf(string key) => Find(key).ValueType;

        // The value in effect: your override (or another mod's) if one is set, else the player's cfg value.
        public T Get<T>(string key)
        {
            object v = Find(key).Current;
            if (!(v is T t))
            {
                throw new ArgumentException($"{key} is a {Find(key).ValueType.Name}, not a {typeof(T).Name}.");
            }
            return t;
        }

        // Override a value for this session (everyone's value, under an instance's own). A value belongs to the
        // owner that set it (docs/mounts-foundation-2.md 2.1): another owner's Set is refused, said once, and the value
        // in effect is returned; TrySet says whether it was set.
        public T Set<T>(string ownerId, string key, T value)
        {
            TrySet(ownerId, key, value, out T applied);
            return applied;
        }

        private static readonly HashSet<string> refusedLogged = new HashSet<string>(StringComparer.Ordinal);

        public bool TrySet<T>(string ownerId, string key, T value, out T applied)
        {
            if (string.IsNullOrEmpty(ownerId))
            {
                throw new ArgumentException("An owner id (your plugin GUID) is required.", nameof(ownerId));
            }
            ITuned t = Find(key);
            if (t.HasOverride && !InstanceTuning.MayReplace(t.OverrideOwner, ownerId))
            {
                if (refusedLogged.Add(Section + "|" + t.Key + "|" + ownerId)) Plugin.Log.LogInfo($"Mount: {ownerId} wants to set {Section}.{t.Key}, which {t.OverrideOwner} has set for this session; left to it (a value belongs to the owner that set it).");
                applied = Get<T>(key);
                return false;
            }
            applied = (T)t.SetOverride(value, ownerId);
            Mount.OnTuningChanged(Style);
            return true;
        }

        public bool IsOverridden(string key) => Find(key).HasOverride;

        // Owner id of the override on this key, or null.
        public string OverriddenBy(string key) => Find(key).OverrideOwner;

        // Back to the player's value for this key: this owner's override cleared; another owner's stays, said once.
        // Returns whether an override was cleared.
        public bool Clear(string ownerId, string key)
        {
            if (string.IsNullOrEmpty(ownerId))
            {
                throw new ArgumentException("An owner id (your plugin GUID) is required.", nameof(ownerId));
            }
            ITuned t = Find(key);
            if (!t.HasOverride) return false;
            if (!InstanceTuning.MayReplace(t.OverrideOwner, ownerId))
            {
                if (refusedLogged.Add(Section + "|" + t.Key + "|" + ownerId + "|clear")) Plugin.Log.LogInfo($"Mount: {ownerId} wants to clear {Section}.{t.Key}, which {t.OverrideOwner} has set for this session; left to it.");
                return false;
            }
            t.ClearOverride();
            Mount.OnTuningChanged(Style);
            return true;
        }

        // Every override of this style, from every mod.
        public void Reset()
        {
            foreach (ITuned t in byKey.Values)
            {
                t.ClearOverride();
            }
            Mount.OnTuningChanged(Style);
        }

        // The dev panel's and the self-tests' way past the owner rule: whoever set it, the key follows the player again.
        internal void ClearAny(string key)
        {
            Find(key).ClearOverride();
            Mount.OnTuningChanged(Style);
        }

        internal void ClearOwner(string ownerId)
        {
            bool any = false;
            foreach (ITuned t in byKey.Values)
            {
                if (t.HasOverride && t.OverrideOwner == ownerId)
                {
                    t.ClearOverride();
                    any = true;
                }
            }
            if (any)
            {
                Mount.OnTuningChanged(Style);
            }
        }

        internal bool TryFind(string key, out ITuned t)
        {
            t = null;
            return key != null && byKey.TryGetValue(key, out t);
        }

        private ITuned Find(string key)
        {
            if (key == null || !byKey.TryGetValue(key, out ITuned t))
            {
                throw new ArgumentException($"No tuning value '{key}'. See MountTuning.Keys.", nameof(key));
            }
            return t;
        }
    }
}
