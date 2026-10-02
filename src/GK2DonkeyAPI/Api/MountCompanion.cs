using System;
using System.Collections.Generic;
using UnityEngine;

namespace GK2DonkeyAPI.Api
{
    // Riding speed steps (MountCompanion.Speed): each a tested speed per style.
    public enum MountSpeed { Normal, Faster, Fastest }

    // Look presets (MountCompanion): Game is the game's own art, Custom uses the matching Custom* hex colour.
    public enum CoatColor { Game, Brown, Dark, Cream, Black, Custom }
    public enum HarnessColor { Game, Black, Red, Blue, Green, Custom }
    public enum CartPaint { Game, DarkWood, Red, Green, Blue, Custom }
    public enum LanternColor { Game, White, Green, Blue, Custom }

    // A mod's own donkey for the keeper, with its cart and an optional foal: everything a player mod needs, so the mod
    // itself only maps its keys and settings onto this (Saddle Up is the example). One per owner (Mounts.Companion).
    //
    // - Use(Ride / Cart), for the ride and cart keys: on foot the donkey (spawned the first time) comes to the keeper
    //   and he gets on, the cart brought and hitched or left standing as asked; mounted in that style he gets off (the
    //   donkey stays); mounted in the other style the cart is left standing or brought and hitched.
    // - SendAway: the donkey, its cart and foal go; the cart's cargo is set down first.
    // - Found again after a load by their keys "donkey", "cart" and "foal" (kept with the save while KeepWithSaves).
    // - The settings below apply as they're set. Looks, the name and the foal belong to this companion's own donkey,
    //   cart and foal only (instance overrides, set by the owner); speed, brays, the bell, the volume and the lantern
    //   are the ride's (session overrides of both styles, set by the owner), since a style value can't differ per
    //   instance. Values another mod set are never cleared.
    public sealed class MountCompanion
    {
        public const string DonkeyKey = "donkey", CartKey = "cart", FoalKey = "foal";

        private static readonly Dictionary<string, MountCompanion> all = new Dictionary<string, MountCompanion>(StringComparer.Ordinal);

        internal static MountCompanion For(string ownerId)
        {
            if (string.IsNullOrEmpty(ownerId))
            {
                throw new ArgumentException("An owner id (your plugin GUID) is required.", nameof(ownerId));
            }
            if (!all.TryGetValue(ownerId, out MountCompanion c))
            {
                all[ownerId] = c = new MountCompanion(ownerId);
            }
            return c;
        }

        internal static void TickAll()
        {
            foreach (MountCompanion c in all.Values)
            {
                try
                {
                    c.Tick();
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"Companion of {c.OwnerId}: {e.Message}");
                }
            }
        }

        public string OwnerId { get; }

        // A donkey on paper: never spawned or registered, it only carries this companion's look, so the settings can
        // show the colours (MountSettingsPage.Preview) in the main menu, indoors, or before the donkey was ever called.
        private Mount.Instance paper;

        internal Mount.Instance Paper
        {
            get
            {
                if (paper == null)
                {
                    // With its cart: it's drawn with one (PreviewStage) and reads the Cart style's lantern settings.
                    paper = new Mount.Instance(0, OwnerId, ephemeral: true, hasDonkey: true, hasCart: true) { OwnLook = true };
                    ApplyLook();
                }
                return paper;
            }
        }

        // Its donkey (with its cart while hitched), its cart while that stands on its own, and its foal. Null when none.
        public MountHandle Donkey { get; private set; }
        public MountHandle Cart { get; private set; }
        public MountHandle Foal { get; private set; }

        private MountCompanion(string ownerId)
        {
            OwnerId = ownerId;
            Mounts.GameReady += OnGameReady;
            Mounts.InstanceRemoved += OnRemoved;
            Mounts.Hitched += OnHitched;
            Mounts.Unhitched += OnUnhitched;
            if (Mounts.IsGameReady)
            {
                OnGameReady();
            }
        }

        // ------------------------------------------------------------------ settings

        private bool keep = true, foal, brays = true, bell = true, lantern = true, lanternByDay = true;
        private string name;
        private MountSpeed speed;
        private float volume = 1f;
        private CoatColor coat;
        private HarnessColor harness;
        private CartPaint paint;
        private LanternColor lanternColor;
        private string customCoat, customHarness, customPaint, customLantern;

        // Kept with the save: after a load they're back where they were left, cargo on the cart.
        public bool KeepWithSaves { get => keep; set { keep = value; ApplyKeep(); } }

        // A foal (size 0.6, not rideable) that stays with the donkey.
        public bool HasFoal { get => foal; set { foal = value; nextFoalCheck = 0f; } }

        // Shown in the donkey's prompt ("Ride Bella"); empty for none.
        public string Name { get => name; set { name = value; ApplyName(); } }

        public MountSpeed Speed { get => speed; set { speed = value; ApplyRide(); } }

        // Brays at mounting and while standing.
        public bool Brays { get => brays; set { brays = value; ApplyRide(); } }

        // The bell when the cart sets off with cargo.
        public bool BellOnSetOff { get => bell; set { bell = value; ApplyRide(); } }

        // The mount's sounds, times the game's SFX volume: 0 to 1.5.
        public float SoundVolume { get => volume; set { volume = Mathf.Clamp(value, 0f, 1.5f); ApplyRide(); } }

        // The lantern pole and lamp on the cart.
        public bool Lantern { get => lantern; set { lantern = value; ApplyRide(); } }

        // The lamp is lit by day too (the game's cart lamp is); off: it goes dark by day like the hand lantern.
        public bool LanternByDay { get => lanternByDay; set { lanternByDay = value; ApplyRide(); } }

        public CoatColor Coat { get => coat; set { coat = value; ApplyLook(); } }
        public HarnessColor Harness { get => harness; set { harness = value; ApplyLook(); } }
        public CartPaint Paint { get => paint; set { paint = value; ApplyLook(); } }
        public LanternColor LanternLight { get => lanternColor; set { lanternColor = value; ApplyLook(); } }

        // The colours used with Custom, hex RRGGBB (a leading # is fine). Invalid: the game's look, logged once.
        public string CustomCoat { get => customCoat; set { customCoat = value; ApplyLook(); } }
        public string CustomHarness { get => customHarness; set { customHarness = value; ApplyLook(); } }
        public string CustomPaint { get => customPaint; set { customPaint = value; ApplyLook(); } }
        public string CustomLantern { get => customLantern; set { customLantern = value; ApplyLook(); } }

        // ------------------------------------------------------------------ the keys

        private MountStyle? pending;
        private float pendingUntil;
        private const float PendingSeconds = 5f;

        // The ride key (Ride) or the cart key (Cart). Ok: done, or under way (the donkey is coming). A refusal is also
        // written to the log, with its reason.
        public MountResult Use(MountStyle style)
        {
            MountResult r = UseNow(style);
            if (!r.Ok)
            {
                Plugin.Log.LogInfo($"{OwnerId}: {style} not now: {r.Reason}");
            }
            return r;
        }

        // The bell, while mounted (else nothing).
        public void RingBell()
        {
            if (Mounts.IsMounted)
            {
                Mounts.RingBell();
            }
        }

        private MountResult UseNow(MountStyle style)
        {
            if (!Mounts.IsAvailable)
            {
                return MountResult.Refused("GK2 Donkey API is off.");
            }
            if (Mounts.IsMounted)
            {
                return UseWhileMounted(style);
            }
            if (Donkey == null || !Donkey.Exists)
            {
                // Placed by ComeToKeeper right below.
                Donkey = Mounts.Spawn(OwnerId, new MountSpawn { Position = Vector3.zero, Key = DonkeyKey, Persist = keep });
                if (Donkey == null)
                {
                    return MountResult.Refused("The donkey could not be spawned (see the log).");
                }
                Plugin.Log.LogInfo($"Companion: spawned {Donkey.FullId} for {OwnerId}.");
                ApplyAll();
            }
            MountResult come = Donkey.ComeToKeeper();
            if (!come.Ok)
            {
                return come;
            }
            pending = style;
            pendingUntil = Time.unscaledTime + PendingSeconds;
            ContinuePending();
            return MountResult.Success;
        }

        private MountResult UseWhileMounted(MountStyle style)
        {
            if (Mounts.Style == style)
            {
                Mounts.Dismount("dismounted");
                return MountResult.Success;
            }
            MountHandle riding = Mounts.Riding;
            if (riding == null)
            {
                return Mounts.SwitchStyle(style); // another mod's summon (Mounts.TryMount): its own way
            }
            if (style == MountStyle.Ride)
            {
                // Leaving the cart: refused, the keeper says why (as the hitch key does), e.g. no room to set it down.
                MountHandle left = Mounts.UnhitchRidden(out string notLeft);
                if (left == null)
                {
                    notLeft = string.IsNullOrEmpty(notLeft) ? "The cart could not be left here." : notLeft;
                    Mount.Say(notLeft);
                    return MountResult.Refused(notLeft);
                }
                return MountResult.Success;
            }
            string why = BringCart(riding);
            return why == null ? MountResult.Success : MountResult.Refused(why);
        }

        // Its cart (spawned the first time) comes to where the donkey stands or is ridden, and is hitched.
        private string BringCart(MountHandle to)
        {
            if (Cart == null || !Cart.Exists)
            {
                Cart = Mounts.Spawn(OwnerId, new MountSpawn { Position = Vector3.zero, Donkey = false, WithCart = true, Key = CartKey, Persist = keep });
                if (Cart == null)
                {
                    return "The cart could not be spawned.";
                }
                ApplyLook();
            }
            MountResult come = Cart.ComeToKeeper(to.FacingRight);
            if (!come.Ok)
            {
                return come.Reason;
            }
            MountResult hitched = Mounts.Hitch(to, Cart);
            return hitched.Ok ? null : hitched.Reason;
        }

        private void ContinuePending()
        {
            if (Time.unscaledTime > pendingUntil || Donkey == null || !Donkey.Exists)
            {
                if (Donkey != null && Donkey.Exists)
                {
                    Plugin.Log.LogInfo("Companion: the donkey did not become ready in time; press the key again.");
                }
                pending = null;
                return;
            }
            if (!Donkey.IsReady)
            {
                return; // its view is loading (a spawn, a move into this scene, a hitch or an unhitch)
            }
            MountStyle style = pending.Value;
            if (style == MountStyle.Cart && !Donkey.HasCart)
            {
                string why = BringCart(Donkey);
                if (why != null)
                {
                    Plugin.Log.LogInfo("Companion: no cart: " + why);
                    pending = null;
                }
                return; // hitched: a new view, ready in a frame or two
            }
            if (style == MountStyle.Ride && Donkey.HasCart)
            {
                if (Mounts.Unhitch(Donkey) == null)
                {
                    pending = null;
                }
                return;
            }
            pending = null;
            Donkey.Ride();
        }

        // The donkey, its cart and foal go for good; what is on a cart is set down where it stands first.
        public void SendAway()
        {
            pending = null;
            if (Donkey != null && Donkey.IsRidden)
            {
                Mounts.Dismount("sent away");
            }
            foreach (MountHandle h in new[] { Foal, Cart, Donkey })
            {
                if (h != null && h.Exists)
                {
                    h.SetDownCargo();
                    h.Remove();
                }
            }
            Donkey = Cart = Foal = null;
            Plugin.Log.LogInfo($"Companion: {OwnerId} sent its donkey away.");
        }

        // ------------------------------------------------------------------ keeping track

        private float nextFoalCheck;

        private void Tick()
        {
            if (pending.HasValue)
            {
                ContinuePending();
            }
            if (Time.unscaledTime >= nextFoalCheck)
            {
                nextFoalCheck = Time.unscaledTime + 1f;
                UpdateFoal();
            }
        }

        private void UpdateFoal()
        {
            if (!Mounts.IsGameReady)
            {
                return;
            }
            if (foal && (Foal == null || !Foal.Exists) && Donkey != null && Donkey.Exists)
            {
                Foal = Mounts.SpawnFoal(OwnerId, Donkey, key: FoalKey, persist: keep);
                if (Foal != null)
                {
                    ApplyLook();
                }
            }
            else if (!foal && Foal != null && Foal.Exists)
            {
                Foal.Remove();
                Foal = null;
            }
        }

        private void OnGameReady()
        {
            pending = null;
            Donkey = Mounts.Find(OwnerId, DonkeyKey);
            Cart = Mounts.Find(OwnerId, CartKey);
            Foal = Mounts.Find(OwnerId, FoalKey);
            if (Donkey != null || Cart != null)
            {
                Plugin.Log.LogInfo($"Companion: {OwnerId} found its {(Donkey != null ? "donkey " + Donkey.FullId : "")}{(Donkey != null && Cart != null ? " and " : "")}{(Cart != null ? "cart " + Cart.FullId : "")}{(Foal != null ? " and foal " + Foal.FullId : "")}.");
            }
            nextFoalCheck = 0f;
            ApplyAll();
        }

        private void OnRemoved(MountHandle h)
        {
            if (h == Donkey) Donkey = null;
            if (h == Cart) Cart = null;
            if (h == Foal) Foal = null;
        }

        private void OnHitched(MountHandle d, MountHandle c)
        {
            if (c == Cart)
            {
                Cart = null; // part of the donkey now
            }
            if (d == Donkey)
            {
                ApplyLook();
            }
        }

        private void OnUnhitched(MountHandle d, MountHandle c)
        {
            if (d != Donkey)
            {
                return;
            }
            Cart = c;
            c.SetKey(OwnerId, CartKey); // the cart left standing is found by its key after a load
            ApplyLook();
        }

        // ------------------------------------------------------------------ applying

        private void ApplyAll()
        {
            ApplyKeep();
            ApplyName();
            ApplyRide();
            ApplyLook();
        }

        private void ApplyKeep()
        {
            foreach (MountHandle h in new[] { Donkey, Cart })
            {
                if (h != null && h.Exists)
                {
                    h.SetPersist(OwnerId, keep); // its foal follows (children of the same owner)
                }
            }
        }

        private void ApplyName()
        {
            if (Donkey != null && Donkey.Exists)
            {
                Donkey.SetDisplayName(OwnerId, name);
            }
        }

        // Speeds per style for Faster and Fastest; Normal leaves the player's own (Ride 1.75, Cart 1.5 as shipped).
        private static readonly float[] RideSpeeds = { 0f, 2.1f, 2.5f };
        private static readonly float[] CartSpeeds = { 0f, 1.8f, 2.1f };

        private void ApplyRide()
        {
            if (!Mounts.IsAvailable)
            {
                return;
            }
            foreach (MountStyle style in new[] { MountStyle.Ride, MountStyle.Cart })
            {
                MountTuning t = Mounts.Tuning(style);
                float s = (style == MountStyle.Ride ? RideSpeeds : CartSpeeds)[(int)speed];
                Session(t, "SpeedMultiplier", s > 0f, s);
                Session(t, "BrayOnMountChance", !brays, 0f);
                Session(t, "BrayIdleEverySeconds", !brays, 0f);
                Session(t, "SoundVolume", !Mathf.Approximately(volume, 1f), volume);
            }
            MountTuning cart = Mounts.Tuning(MountStyle.Cart);
            Session(cart, "BellOnSetOff", !bell, false);
            Session(cart, "LanternShow", !lantern, false);
            Session(cart, "LanternLightDayDim", !lanternByDay, 0f);
        }

        // Set while on; cleared when off, but only if this owner set it (another mod's value stays).
        private void Session<T>(MountTuning t, string key, bool on, T value)
        {
            try
            {
                if (on)
                {
                    t.Set(OwnerId, key, value);
                }
                else if (t.IsOverridden(key) && t.OverriddenBy(key) == OwnerId)
                {
                    t.Clear(key);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Companion: {key} could not be set: {e.Message}");
            }
        }

        private void ApplyLook()
        {
            if (!Mounts.IsAvailable)
            {
                return;
            }
            var coatKeys = Look(CoatPreset(coat, customCoat, "coat"));
            var harnessKeys = Look(HarnessPreset(harness, customHarness, "harness"));
            if (paper != null)
            {
                OnPaper(CoatKeys, coatKeys);
                OnPaper(HarnessKeys, harnessKeys);
            }
            var paperPaint = Look(PaintPreset(paint, customPaint, "cart paint"));
            var paperLantern = Look(LanternPreset(lanternColor, customLantern, "lantern"));
            if (paper != null)
            {
                OnPaper(PaintKeys, paperPaint);
                OnPaper(LanternKeys, paperLantern);
                paper.ColorsRig.MarkDirty();
            }
            var paintKeys = Look(PaintPreset(paint, customPaint, "cart paint"));
            var lanternKeys = Look(LanternPreset(lanternColor, customLantern, "lantern"));
            foreach (MountHandle h in new[] { Donkey, Foal })
            {
                if (h != null && h.Exists)
                {
                    Instance(h, CoatKeys, coatKeys);
                    Instance(h, HarnessKeys, harnessKeys);
                }
            }
            // The cart's colours sit on whichever holds the cart (they move with it at hitch and unhitch).
            MountHandle withCart = Donkey != null && Donkey.Exists && Donkey.HasCart ? Donkey : Cart != null && Cart.Exists ? Cart : null;
            if (withCart != null)
            {
                Instance(withCart, PaintKeys, paintKeys);
                Instance(withCart, LanternKeys, lanternKeys);
            }
        }

        private static Dictionary<string, object> Look(Dictionary<string, object> d) => d ?? new Dictionary<string, object>();

        // The paper donkey's own values, as an instance override would be (the values are already of the key's type).
        private void OnPaper(string[] keys, Dictionary<string, object> values)
        {
            foreach (string key in keys)
            {
                if (values.TryGetValue(key, out object v))
                {
                    paper.Overrides[key] = v;
                    paper.OverrideOwners[key] = OwnerId;
                }
                else
                {
                    paper.Overrides.Remove(key);
                    paper.OverrideOwners.Remove(key);
                }
            }
        }

        // Every key a part may set: cleared (if this owner set it) when the preset doesn't use it.
        private static readonly string[] CoatKeys = { "CoatColorize", "CoatColorizeAmount", "CoatBrightness", "CoatSaturation" };
        private static readonly string[] HarnessKeys = { "HarnessColorize", "HarnessColorizeAmount", "HarnessBrightness", "HarnessSaturation" };
        private static readonly string[] PaintKeys =
        {
            "CartBodyColorize", "CartBodyColorizeAmount", "CartBodyBrightness",
            "ShaftsColorize", "ShaftsColorizeAmount", "ShaftsBrightness",
            "WheelsBrightness",
        };
        private static readonly string[] LanternKeys = { "LanternLightColor", "GlowColor", "LitGlassTint" };

        private void Instance(MountHandle h, string[] keys, Dictionary<string, object> values)
        {
            if (!h.I.OwnLook)
            {
                h.I.OwnLook = true; // a preset is the whole look: "Game" really is the game's art
                h.I.ColorsRig.MarkDirty();
            }
            foreach (string key in keys)
            {
                try
                {
                    if (values.TryGetValue(key, out object v))
                    {
                        if (v is string s)
                        {
                            h.Tuning.Set(OwnerId, key, s);
                        }
                        else
                        {
                            h.Tuning.Set(OwnerId, key, (float)v);
                        }
                    }
                    else if (h.Tuning.IsSet(key) && h.Tuning.SetBy(key) == OwnerId)
                    {
                        h.Tuning.Clear(key);
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"Companion: {key} on {h.FullId} could not be set: {e.Message}");
                }
            }
        }

        // ------------------------------------------------------------------ the presets (the API's, so every player mod looks the same)

        private static Dictionary<string, object> CoatPreset(CoatColor c, string custom, string what)
        {
            switch (c)
            {
                case CoatColor.Brown: return new Dictionary<string, object> { ["CoatColorize"] = "8A5A36", ["CoatColorizeAmount"] = 0.85f };
                case CoatColor.Dark: return new Dictionary<string, object> { ["CoatBrightness"] = 0.6f };
                case CoatColor.Cream: return new Dictionary<string, object> { ["CoatColorize"] = "E0CFA8", ["CoatColorizeAmount"] = 0.8f, ["CoatBrightness"] = 1.15f };
                case CoatColor.Black: return new Dictionary<string, object> { ["CoatBrightness"] = 0.35f, ["CoatSaturation"] = 0.2f };
                case CoatColor.Custom:
                    string hex = Hex(custom, what);
                    return hex == null ? null : new Dictionary<string, object> { ["CoatColorize"] = hex, ["CoatBrightness"] = Lightness(hex, "Coat") };
                default: return null;
            }
        }

        private static Dictionary<string, object> HarnessPreset(HarnessColor c, string custom, string what)
        {
            switch (c)
            {
                case HarnessColor.Black: return new Dictionary<string, object> { ["HarnessBrightness"] = 0.35f, ["HarnessSaturation"] = 0.3f };
                case HarnessColor.Red: return new Dictionary<string, object> { ["HarnessColorize"] = "A8322D" };
                case HarnessColor.Blue: return new Dictionary<string, object> { ["HarnessColorize"] = "34558F" };
                case HarnessColor.Green: return new Dictionary<string, object> { ["HarnessColorize"] = "3F6E3A" };
                case HarnessColor.Custom:
                    string hex = Hex(custom, what);
                    return hex == null ? null : new Dictionary<string, object> { ["HarnessColorize"] = hex, ["HarnessBrightness"] = Lightness(hex, "Harness") };
                default: return null;
            }
        }

        private static Dictionary<string, object> PaintPreset(CartPaint c, string custom, string what)
        {
            string tint = null;
            switch (c)
            {
                case CartPaint.DarkWood:
                    return new Dictionary<string, object> { ["CartBodyBrightness"] = 0.6f, ["ShaftsBrightness"] = 0.6f, ["WheelsBrightness"] = 0.6f };
                case CartPaint.Red: tint = "9C3B2E"; break;
                case CartPaint.Green: tint = "4C6B3A"; break;
                case CartPaint.Blue: tint = "3E5A86"; break;
                case CartPaint.Custom: tint = Hex(custom, what); break;
            }
            if (tint == null)
            {
                return null;
            }
            var d = new Dictionary<string, object>
            {
                ["CartBodyColorize"] = tint, ["CartBodyColorizeAmount"] = 0.75f,
                ["ShaftsColorize"] = tint, ["ShaftsColorizeAmount"] = 0.75f,
            };
            if (c == CartPaint.Custom)
            {
                d["CartBodyColorizeAmount"] = d["ShaftsColorizeAmount"] = 1f;
                d["CartBodyBrightness"] = Lightness(tint, "CartBody");
                d["ShaftsBrightness"] = Lightness(tint, "Shafts");
            }
            return d;
        }

        private static Dictionary<string, object> LanternPreset(LanternColor c, string custom, string what)
        {
            string hex;
            switch (c)
            {
                case LanternColor.White: hex = "FFF1DA"; break;
                case LanternColor.Green: hex = "86FF8C"; break;
                case LanternColor.Blue: hex = "8CC2FF"; break;
                case LanternColor.Custom: hex = Hex(custom, what); break;
                default: return null;
            }
            return hex == null ? null : new Dictionary<string, object> { ["LanternLightColor"] = hex, ["GlowColor"] = hex, ["LitGlassTint"] = hex };
        }

        // Colorize keeps each pixel's own brightness, so a custom colour also sets the brightness: how light the colour
        // is against how light the region's art is (Black comes out dark, Snow light).
        private static float Lightness(string hex, string region)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out Color c);
            Color.RGBToHSV(c, out _, out _, out float v);
            float art = Mathf.Max(0.05f, Colors.ArtLightness(region));
            return Mathf.Clamp(v / art, 0.15f, 3f);
        }

        private static readonly HashSet<string> warned = new HashSet<string>(StringComparer.Ordinal);

        // RRGGBB, upper case, or null (logged once per bad value).
        private static string Hex(string value, string what)
        {
            string v = (value ?? "").Trim().TrimStart('#');
            bool ok = v.Length == 6;
            for (int i = 0; ok && i < 6; i++)
            {
                ok = Uri.IsHexDigit(v[i]);
            }
            if (ok)
            {
                return v.ToUpperInvariant();
            }
            if (warned.Add(what + "|" + value))
            {
                Plugin.Log.LogWarning($"Companion: the custom {what} colour '{value}' isn't a hex colour (RRGGBB); the game's look is used.");
            }
            return null;
        }
    }
}
