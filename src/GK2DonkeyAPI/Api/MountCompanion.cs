using System;
using System.Collections.Generic;
using UnityEngine;

namespace GK2DonkeyAPI.Api
{
    // Look presets (MountCompanion): Game is the game's own art, Custom uses the matching Custom* hex colour.
    public enum CoatColor { Game, Brown, Dark, Cream, Black, Custom }
    public enum HarnessColor { Game, Black, Red, Blue, Green, Custom }
    // The second body tone (the API's Muzzle region): the donkey's muzzle, another mount's tail, ears and nose, or
    // shell, a skin's glow. A mount whose look has no such region ignores it (MountSettingsPage.MountHasRegion greys
    // the row). The presets are chosen to differ from every part's own hue (a rose on a pink part and a brown on
    // a brown shell were invisible at first, the user 2026-10-20): each moves every part by a clear step.
    public enum MuzzleColor { Game, Pale, Rose, Brown, Green, Blue, Dark, Custom }
    public enum CartPaint { Game, DarkWood, Red, Green, Blue, Custom }
    public enum LanternColor { Game, White, Green, Blue, Custom }
    public enum WheelColor { Game, DarkWood, Red, Green, Blue, Black, Custom }
    public enum HubColor { Game, Black, Brass, Silver, Custom }

    // The look of one mount, as the colour rows set it (MountCompanion.LookOf; docs/mount-colours.md 2.7): the mount's
    // coat and harness, and the cart's paint, wheels, hubs and lantern light as they are with that mount. Setting a
    // value applies it at once when this is the current mount's look; otherwise it waits for the mount to be chosen.
    public sealed class MountLook
    {
        private readonly MountCompanion owner;
        public string MountId { get; }

        internal MountLook(MountCompanion owner, string mountId)
        {
            this.owner = owner;
            MountId = mountId;
        }

        private CoatColor coat;
        private HarnessColor harness;
        private MuzzleColor muzzle;
        private string customMuzzle;
        private CartPaint paint;
        private LanternColor lanternColor;
        private string customCoat, customHarness, customPaint, customLantern, customWheels, customHubs;
        private WheelColor wheels;
        private HubColor hubs;

        public CoatColor Coat { get => coat; set { coat = value; owner.LookChanged(this); } }
        public HarnessColor Harness { get => harness; set { harness = value; owner.LookChanged(this); } }
        public MuzzleColor Muzzle { get => muzzle; set { muzzle = value; owner.LookChanged(this); } }
        public string CustomMuzzle { get => customMuzzle; set { customMuzzle = value; owner.LookChanged(this); } }
        public CartPaint Paint { get => paint; set { paint = value; owner.LookChanged(this); } }
        public LanternColor LanternLight { get => lanternColor; set { lanternColor = value; owner.LookChanged(this); } }
        public string CustomCoat { get => customCoat; set { customCoat = value; owner.LookChanged(this); } }
        public string CustomHarness { get => customHarness; set { customHarness = value; owner.LookChanged(this); } }
        public string CustomPaint { get => customPaint; set { customPaint = value; owner.LookChanged(this); } }
        public WheelColor Wheels { get => wheels; set { wheels = value; owner.LookChanged(this); } }
        public HubColor Hubs { get => hubs; set { hubs = value; owner.LookChanged(this); } }
        public string CustomWheels { get => customWheels; set { customWheels = value; owner.LookChanged(this); } }
        public string CustomHubs { get => customHubs; set { customHubs = value; owner.LookChanged(this); } }
        public string CustomLantern { get => customLantern; set { customLantern = value; owner.LookChanged(this); } }

        // The mount's own settings beside its colours (the user, 2026-10-26: the babies, the name, the voice and the cart's
        // lantern kept per mount, set in the Mounts panel's journal): set at once when this is the current mount's.
        // A mount nobody set them for takes the companion's last values (MountCompanion.FoalCount etc.).
        internal int foals;
        internal bool foalsFollow, foalsIndoors, foalsWearSkin = true, lantern = true, lanternByDay = true;
        internal string name;
        internal float volume = 1f, stepsVolume = 1f, babyStepsVolume = 1f;

        public int FoalCount { get => foals; set { foals = Mathf.Clamp(value, 0, MountCompanion.MaxFoals); owner.OwnChanged(this); } }
        public bool FoalsFollowOnFoot { get => foalsFollow; set { foalsFollow = value; owner.OwnChanged(this); } }
        public bool FoalsComeIndoors { get => foalsIndoors; set { foalsIndoors = value; owner.OwnChanged(this); } }
        public bool FoalsWearSkin { get => foalsWearSkin; set { foalsWearSkin = value; owner.OwnChanged(this); } }
        public string Name { get => name; set { name = value; owner.OwnChanged(this); } }
        // Its voices (calls, its babies' answers; 0.40.0: the voices only, before every sound), its steps and the rest, its
        // babies' steps: 0 to 1.5 each, times the player's SoundVolume (the API cfg's VoicesVolume, StepsVolume,
        // BabyStepsVolume while this is the current mount's).
        public float SoundVolume { get => volume; set { volume = Mathf.Clamp(value, 0f, 1.5f); owner.OwnChanged(this); } }
        public float StepsVolume { get => stepsVolume; set { stepsVolume = Mathf.Clamp(value, 0f, 1.5f); owner.OwnChanged(this); } }
        public float BabyStepsVolume { get => babyStepsVolume; set { babyStepsVolume = Mathf.Clamp(value, 0f, 1.5f); owner.OwnChanged(this); } }
        public bool Lantern { get => lantern; set { lantern = value; owner.OwnChanged(this); } }
        public bool LanternByDay { get => lanternByDay; set { lanternByDay = value; owner.OwnChanged(this); } }
    }

    // A mod's own donkey for the keeper, with its cart and up to MaxFoals foals: everything a player mod needs, so the mod
    // itself only maps its keys and settings onto this. One per owner (Mounts.Companion).
    //
    // - Use(Ride / Cart), for the ride and cart keys: on foot the donkey (spawned the first time) comes to the keeper
    //   and he gets on, the cart brought and hitched (Cart) or, when the donkey stands hitched to it somewhere, left
    //   there where it was parked (Ride, CartStaysParked; off: it comes along and is left by the keeper); mounted in
    //   that style he gets off (the donkey stays); mounted in the other style the cart is left standing or brought and
    //   hitched.
    // - SendAway: the donkey, its cart and foals go; the cart's cargo is set down first.
    // - Found again after a load by their keys "donkey", "cart" and "foal", "foal2" ... (kept with the save while
    //   KeepWithSaves).
    // - The settings below apply as they're set; their starting values are the defaults a mod's settings should use
    //   (e.g. p.Toggle(..., me.BellOnSetOff, ...)), so they're written once, here. Looks, the name and the foals belong
    //   to this companion's own donkey, cart and foals only (instance overrides, set by the owner); the speeds, brays,
    //   the bell, the volume and the lantern
    //   are the ride's (session overrides of both styles, set by the owner), since a style value can't differ per
    //   instance. Values another mod set are never cleared.
    public sealed class MountCompanion
    {
        public const string DonkeyKey = "donkey", CartKey = "cart", FoalKey = "foal";

        // FoalCount at most.
        public const int MaxFoals = 5;

        // The speeds are each mount's own since 0.36.0 (MountDef.Speed, a pack's [Mount] speed):
        // the companion's speed steps (RideSpeed, CartSpeed, SpeedLabels) are gone.

        // The first foal keeps the key "foal" (as before there were several); the others "foal2" to "foal5".
        internal static string FoalKeyOf(int i) => i == 0 ? FoalKey : FoalKey + (i + 1);

        private static readonly Dictionary<string, MountCompanion> all = new Dictionary<string, MountCompanion>(StringComparer.Ordinal);
        internal static int Count => all.Count;

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

        // Every companion's paper donkey made so far (Looks.MarkWearersDirty).
        internal static void ForEachPaper(Action<Mount.Instance> act)
        {
            foreach (MountCompanion c in all.Values)
            {
                if (c.paper != null) act(c.paper);
            }
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
                    Said.Warn("companion " + c.OwnerId + " " + e.Message, $"Companion of {c.OwnerId}: {e.Message}");
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

        // Its donkey (with its cart while hitched), its cart while that stands on its own, and its foals (a slot each,
        // null when that foal isn't there). Null when none.
        public MountHandle Donkey { get; private set; }
        public MountHandle Cart { get; private set; }

        // The donkey pulls a cart this companion did not summon (hitched in the world, 2026-10-14): that cart keeps its
        // own look (the paint, wheels, hubs and lantern colours are this companion's own cart's), and when it is left
        // standing again it stays a cart of its own, not this companion's. Kept in the donkey's data for a load.
        private bool foreignCart;
        private const string CartDataKey = "cart";
        public IReadOnlyList<MountHandle> Foals => foalHandles;

        private readonly MountHandle[] foalHandles = new MountHandle[MaxFoals];

        // Each baby its own (FoalVariety.cs): its size, shade and temperament from the family's salt (kept in the
        // parent's data, made once, saved with it), the mount it is a baby of and its place among its siblings.
        private const string FoalSaltKey = "foalsalt";
        private string foalSalt;
        private MountHandle foalSaltOf;
        private float varietyApplied = -1f;

        private string FoalSalt()
        {
            if (Donkey == null || !Donkey.Exists)
            {
                return "";
            }
            if (!ReferenceEquals(foalSaltOf, Donkey) || foalSalt == null)
            {
                foalSaltOf = Donkey;
                foalSalt = Donkey.GetData(OwnerId, FoalSaltKey);
                if (string.IsNullOrEmpty(foalSalt))
                {
                    foalSalt = Guid.NewGuid().ToString("N").Substring(0, 12);
                    Donkey.SetData(OwnerId, FoalSaltKey, foalSalt);
                }
            }
            return foalSalt;
        }

        private static float VarietyNow => IdleShuffle.FoalVariety != null ? IdleShuffle.FoalVariety.Value : 1f;

        // The mount a baby is a baby of: the parent's own when they wear its skin (and it has babies), else the donkey.
        private string BabyMountId()
        {
            MountInfo m = Donkey != null && Donkey.Exists && Current.FoalsWearSkin ? Mounts.Catalog.Find(Donkey.Mount) : null;
            return m != null && m.Foals ? m.Id : MountCatalog.DonkeyId;
        }

        private FoalLooks LooksOfFoal(int i) => FoalLooks.For(FoalSalt(), BabyMountId(), i, VarietyNow);

        internal FoalLooks TestLooksOfFoal(int i) => LooksOfFoal(i); // the self-test

        private MountCompanion(string ownerId)
        {
            OwnerId = ownerId;
            Mounts.GameReady += OnGameReady;
            Mounts.InstanceRemoved += OnRemoved;
            Looks.LoadedReady += id =>
            {
                // the skin's own colours are known now: the custom colours' lightness anchors to them
                if (!string.IsNullOrEmpty(skin) && string.Equals(id, skin.Trim(), StringComparison.OrdinalIgnoreCase)) ApplyLook();
            };
            Mounts.Hitched += OnHitched;
            Mounts.Unhitched += OnUnhitched;
            if (Mounts.IsGameReady)
            {
                OnGameReady();
            }
        }

        // ------------------------------------------------------------------ settings

        private bool keep = true, brays = true, bell, lantern = true, lanternByDay = true, foalsIndoors, foalsFollow;
        private int foals;
        private string name;
        private float volume = 1f;
        // The look (the colour rows) per mount: another mount stays green when the donkey is brown (docs/mount-colours.md;
        // the user, 2026-10-18). Coat, Harness... below are the current mount's (MountId): what a page's rows set is
        // the look of the mount they were built for (MountSettingsPage.PerMount).
        private readonly Dictionary<string, MountLook> looks = new Dictionary<string, MountLook>(StringComparer.OrdinalIgnoreCase);

        // The look of a mount by id (made empty, the game's own colours, the first time it is asked for).
        public MountLook LookOf(string mountId)
        {
            string id = string.IsNullOrWhiteSpace(mountId) ? MountCatalog.DonkeyId : mountId.Trim();
            if (!looks.TryGetValue(id, out MountLook l))
            {
                looks[id] = l = new MountLook(this, id)
                {
                    foals = foals, foalsFollow = foalsFollow, foalsIndoors = foalsIndoors, foalsWearSkin = foalsWearSkin,
                    name = name, volume = volume, lantern = lantern, lanternByDay = lanternByDay,
                };
            }
            return l;
        }

        // The mount the companion's skin is a look of (the donkey without a skin, or with one that isn't installed).
        public string MountId => MountOfSkin(skin);

        private static string MountOfSkin(string skinId)
        {
            string s = (skinId ?? "").Trim();
            if (s.Length == 0 || s.Equals(MountLooks.DonkeyId, StringComparison.OrdinalIgnoreCase)) return MountCatalog.DonkeyId;
            return Mounts.Looks.Find(s)?.Mount ?? MountCatalog.DonkeyId;
        }

        private MountLook Current => LookOf(MountId);

        // One of a mount's own settings changed: applied when it is the current mount's.
        internal void OwnChanged(MountLook l)
        {
            if (string.Equals(l.MountId, MountId, StringComparison.OrdinalIgnoreCase)) ApplyMountOwn();
        }

        // The current mount's own settings onto its donkey, cart and foals (its name, voice, lantern, the babies): at every
        // change of them and of the mount.
        private void ApplyMountOwn()
        {
            nextFoalCheck = 0f;
            ApplyName();
            ApplyRide();
            ApplyFoals();
            ApplyLook();
        }

        internal void LookChanged(MountLook l)
        {
            if (string.Equals(l.MountId, MountId, StringComparison.OrdinalIgnoreCase) || string.Equals(l.MountId, MountOfSkin(PaperSkin), StringComparison.OrdinalIgnoreCase)) ApplyLook();
        }
        private string skin = "";
        private bool foalsWearSkin = true;

        // Kept with the save: after a load they're back where they were left, cargo on the cart.
        public bool KeepWithSaves { get => keep; set { keep = value; ApplyKeep(); } }

        // Use(Ride) while the donkey stands hitched to its cart somewhere: on, the cart is left where it was parked (with
        // its load and chest) and the donkey alone comes; off, both come and the cart is left standing by the keeper.
        // On to start with.
        public bool CartStaysParked { get; set; } = true;

        // How many foals (size 0.6, not rideable) stay with the donkey: 0 to MaxFoals.
        // Per mount since 2026-10-26 (MountLook): the current mount's; setting it also gives a mount nobody set yet this value.
        public int FoalCount { get => Current.FoalCount; set { foals = Mathf.Clamp(value, 0, MaxFoals); Current.FoalCount = foals; } }

        // The foals come through doors with the keeper (buildings, the church, dungeons), follow him inside and come back
        // out with him (the Shuffle key ShuffleFollowKeeperIndoors on each foal). Off to start with: they stay outside
        // with the donkey.
        public bool FoalsComeIndoors { get => Current.FoalsComeIndoors; set { foalsIndoors = value; Current.FoalsComeIndoors = value; } }

        // The foals follow the keeper while he walks on foot outdoors, instead of staying with the mount (the Shuffle key
        // ShuffleFollowKeeperOnFoot on each of this companion's foals, never on another mod's); when he gets on a mount
        // they go back to it (following it when it's theirs). Into buildings only with FoalsComeIndoors. Off to start
        // with (0.36.0); FoalRows' row defaults to it (a mod may switch it on first).
        public bool FoalsFollowOnFoot { get => Current.FoalsFollowOnFoot; set { foalsFollow = value; Current.FoalsFollowOnFoot = value; } }

        // Shown in the donkey's prompt ("Ride Bella"); empty for none.
        public string Name { get => Current.Name; set { name = value; Current.Name = value; } }

        // Brays at mounting and while standing.
        public bool Brays { get => brays; set { brays = value; ApplyRide(); } }

        // The bell when the cart sets off with cargo (off to start with).
        public bool BellOnSetOff { get => bell; set { bell = value; ApplyRide(); } }

        // The mount's sounds, times the game's SFX volume: 0 to 1.5.
        public float SoundVolume { get => Current.SoundVolume; set { volume = Mathf.Clamp(value, 0f, 1.5f); Current.SoundVolume = volume; } }

        // The lantern pole and lamp on the cart.
        public bool Lantern { get => Current.Lantern; set { lantern = value; Current.Lantern = value; } }

        // The lamp is lit by day too (the game's cart lamp is); off: it goes dark by day like the hand lantern.
        public bool LanternByDay { get => Current.LanternByDay; set { lanternByDay = value; Current.LanternByDay = value; } }

        // The current mount's look (LookOf(MountId)); a page's rows set each mount's own (MountSettingsPage.PerMount).
        public CoatColor Coat { get => Current.Coat; set => Current.Coat = value; }
        public HarnessColor Harness { get => Current.Harness; set => Current.Harness = value; }
        public MuzzleColor Muzzle { get => Current.Muzzle; set => Current.Muzzle = value; }
        public CartPaint Paint { get => Current.Paint; set => Current.Paint = value; }
        public LanternColor LanternLight { get => Current.LanternLight; set => Current.LanternLight = value; }

        // The colours used with Custom, hex RRGGBB (a leading # is fine). Empty: the game's look. Invalid: the game's look,
        // logged once.
        public string CustomCoat { get => Current.CustomCoat; set => Current.CustomCoat = value; }
        public string CustomHarness { get => Current.CustomHarness; set => Current.CustomHarness = value; }
        public string CustomMuzzle { get => Current.CustomMuzzle; set => Current.CustomMuzzle = value; }
        public string CustomPaint { get => Current.CustomPaint; set => Current.CustomPaint = value; }

        // The cart's wheels (their wood) and their hubs (the iron fittings), each on its own; "Game" leaves the game's art.
        // The paint no longer touches the wheels (0.19.0).
        public WheelColor Wheels { get => Current.Wheels; set => Current.Wheels = value; }
        public HubColor Hubs { get => Current.Hubs; set => Current.Hubs = value; }
        public string CustomWheels { get => Current.CustomWheels; set => Current.CustomWheels = value; }
        public string CustomHubs { get => Current.CustomHubs; set => Current.CustomHubs = value; }
        public string CustomLantern { get => Current.CustomLantern; set => Current.CustomLantern = value; }

        // The skin the donkey wears, by id (Mounts.Looks.Skins lists them; "donkey", "" or an unknown id: the donkey as the
        // game draws it, and an unknown id is kept, so the skin returns with its pack). Foals wear it too unless
        // FoalsWearSkin is off, or the skin says no foals.
        public string Skin
        {
            get => skin;
            set
            {
                if (panelHeld) { panelSkin = value ?? ""; ApplyLook(); return; }
                skin = value ?? "";
                ApplyMountOwn(); // another mount: its own name, voice, lantern and babies too (each mount keeps its own)
            }
        }

        // The Mounts panel's choice while it is open (the user, 2026-10-22: picking a row changed the mount he had):
        // the settings picture wears it, his donkey keeps its own until Ride or Drive the cart takes it (Commit).
        private bool panelHeld;
        private string panelSkin;

        internal void HoldForPanel()
        {
            panelHeld = true;
            panelSkin = null;
        }

        // The panel closes: with commit the picture's choice becomes his (his donkey changes), without it the choice
        // is dropped (the page puts its Mount row back).
        internal void ReleasePanel(bool commit)
        {
            if (!panelHeld) return;
            panelHeld = false;
            if (commit && panelSkin != null) skin = panelSkin;
            panelSkin = null;
            ApplyMountOwn();
        }

        private string PaperSkin => panelHeld && panelSkin != null ? panelSkin : skin;
        public bool FoalsWearSkin { get => Current.FoalsWearSkin; set { foalsWearSkin = value; Current.FoalsWearSkin = value; } }

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

        // The Mounts panel's Ride and Drive the cart buttons (the user, 2026-10-22: pressed while riding, they got him off,
        // as the keys do for the style he is in). Unlike the keys they never end the ride: already on this companion's
        // mount in that style, he stays on (the mount picked in the panel is already on it, changed in place); in the
        // other style, the keys' switch (the cart left standing, or brought); on someone else's mount, he gets off it
        // and this companion's mount is called in the style.
        internal MountResult UseFromPanel(MountStyle style)
        {
            if (!Mounts.IsAvailable || !Mounts.IsMounted)
            {
                return Use(style);
            }
            MountHandle riding = Mounts.Riding;
            bool mine = riding != null && Donkey != null && Donkey.Exists && riding.I == Donkey.I;
            if (mine && Mounts.Style == style)
            {
                Plugin.Log.LogInfo($"{OwnerId}: {style} from the panel: already on it in that style, the ride goes on.");
                return MountResult.Success;
            }
            if (mine || riding == null)
            {
                return Use(style); // the other style: the keys' switch
            }
            Mounts.Dismount("another mount called");
            if (Mounts.IsMounted)
            {
                return MountResult.Refused("Could not get off the mount ridden now.");
            }
            return Use(style);
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
            if (style == MountStyle.Cart && Mount.Instance.IsInGame(out PlayerController keeper, out _))
            {
                // Asked before anyone is called: the cart refused here (indoors, in a fight) or by the mount (one that pulls
                // none) leaves the mount and the cart where they are, instead of bringing them in and refusing the cart
                // after (BringCart asks the same; the user, 2026-10-26: "although it cant pull it, it still brings it")
                // (a cart holding more than the mount pulls is not refused here: it is brought over and left unhitched, BringCart)
                string noCart = Mount.Instance.WhyNotCart(keeper) ?? (Donkey != null && Donkey.Exists ? GK2DonkeyAPI.Looks.WhyNotCartByMount(Donkey.I) : null);
                if (noCart != null)
                {
                    Mount.Say(noCart);
                    return MountResult.Refused(noCart);
                }
            }
            if (Donkey == null || !Donkey.Exists)
            {
                // Placed by ComeToKeeper right below.
                Donkey = Mounts.Spawn(OwnerId, new MountSpawn { Position = Vector3.zero, Key = DonkeyKey, Persist = keep });
                if (Donkey == null)
                {
                    return MountResult.Refused("Your mount could not be spawned (see the log).");
                }
                Plugin.Log.LogInfo($"Companion: spawned {Donkey.FullId} for {OwnerId}.");
                ApplyAll();
            }
            if (style == MountStyle.Ride && Donkey.HasCart && CartStaysParked)
            {
                // Its cart stays where it was parked and the donkey alone comes (asked for 2026-10-09; before, and with
                // CartStaysParked off, both come and the cart is left standing by the keeper in ContinuePending). Not when
                // the ride here would be refused: then nothing changes and the refusal is shown, as ComeToKeeper shows it.
                if (!Mount.Instance.IsInGame(out PlayerController pc, out _))
                {
                    return MountResult.Refused("Not in a game.");
                }
                string why;
                using (Mount.Bind(Donkey.I))
                {
                    why = Donkey.I.WhyCannotRideHere(pc, showMessage: true, asStyle: MountStyle.Ride);
                }
                if (why != null)
                {
                    return MountResult.Refused(why);
                }
                if (Mounts.Unhitch(Donkey) == null)
                {
                    const string notLeft = "The cart could not be left where it stands.";
                    Mount.Say(notLeft);
                    return MountResult.Refused(notLeft);
                }
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
            // No cart while flying (the flying experiment; the user, 2026-10-23: quietly, no bubble): the cart key in the
            // air does nothing
            if (style == MountStyle.Cart && Mounts.Style != MountStyle.Cart && Flight.InAir)
            {
                return MountResult.Refused("flying");
            }
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
            // Asked before the cart is moved: refused, it stays where it is (in a fight the keeper's armour refuses it; a
            // mount that can't pull the cart refuses it too: it was brought over and the hitch refused it then)
            if (Mount.Instance.IsInGame(out PlayerController keeper, out _))
            {
                string no = Mount.Instance.WhyNotCart(keeper) ?? GK2DonkeyAPI.Looks.WhyNotCartByMount(to?.I);
                if (no != null)
                {
                    Mount.Say(no);
                    return no;
                }
            }
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
            // more on it than this mount pulls (a mount that pulls 4, and the donkey's six): the cart comes over all the same and stands
            // there unhitched, so the extra can be taken off it on foot (the user, 2026-10-27: "still bring the cart to him but
            // dont hitch it")
            string tooMuch = GK2DonkeyAPI.Looks.WhyNotCartByLoad(to.I, Cart.I);
            if (tooMuch != null)
            {
                Mount.Say(tooMuch);
                Plugin.Log.LogInfo($"Companion: the cart came over with {Cart.I.CargoRig.Count} item(s) but stays unhitched: {tooMuch}");
                return tooMuch;
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
                // CartStaysParked off: the cart came along and is left standing here, by the keeper.
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
            var all = new List<MountHandle>(foalHandles) { Cart, Donkey };
            foreach (MountHandle h in all)
            {
                if (h != null && h.Exists)
                {
                    h.SetDownCargo();
                    h.Remove();
                }
            }
            Donkey = Cart = null;
            Array.Clear(foalHandles, 0, foalHandles.Length);
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
            bool spawned = false;
            // A baby's size is its mount's (MountDef.FoalScale: the donkey's 0.6, a larger-drawn mount's 0.33): a baby of another
            // size (the mount changed under it) is made again at the right one.
            // ... and only when the babies are the same mount and skin: plain baby donkeys are 0.6 (the row's promise)
            MountInfo babyMount = Donkey != null && Donkey.Exists && Current.FoalsWearSkin ? Mounts.Catalog.Find(Donkey.Mount) : null;
            float babySize = babyMount != null && babyMount.Foals ? babyMount.FoalScale : 0.6f; // a mount without babies of its own: plain donkeys
            for (int i = 0; i < MaxFoals; i++)
            {
                MountHandle f = foalHandles[i];
                bool wanted = i < Current.FoalCount;
                FoalLooks own = wanted ? LooksOfFoal(i) : FoalLooks.Alike;
                float size = babySize * own.Size; // its own size (FoalVariety.cs)
                // (the view's scale is kept in steps of 0.02: 0.33 comes back as 0.32, so a hundredth is no difference;
                // seen 2026-10-18: a baby made again every second)
                if (wanted && f != null && f.Exists && Math.Abs(f.Scale - size) > 0.015f && Donkey != null && Donkey.Exists)
                {
                    Plugin.Log.LogInfo($"Companion: {OwnerId}: baby {f.FullId} is {f.Scale:F2} of the grown one, the mount '{Donkey.Mount}' wants {size:F2} for it: its view made again at that size.");
                    f.I.Rescale(size); // the same baby, where it stands (not removed and spawned anew)
                }
                if (wanted && (f == null || !f.Exists) && Donkey != null && Donkey.Exists)
                {
                    foalHandles[i] = Mounts.SpawnFoal(OwnerId, Donkey, size, key: FoalKeyOf(i), persist: keep);
                    spawned |= foalHandles[i] != null;
                }
                else if (!wanted && f != null && f.Exists)
                {
                    f.Remove();
                    foalHandles[i] = null;
                }
                f = foalHandles[i];
                if (wanted && f != null && f.Exists)
                {
                    f.I.Temper = own.Temper; // not saved: from the seed again (cheap: a few numbers)
                }
            }
            float variety = VarietyNow;
            if (spawned || Math.Abs(variety - varietyApplied) > 0.001f)
            {
                varietyApplied = variety; // a new strength reaches the shades at once (the sizes above)
                ApplyLook();
                ApplyFoals();
            }
        }

        private void OnGameReady()
        {
            pending = null;
            Donkey = Mounts.Find(OwnerId, DonkeyKey);
            Cart = Mounts.Find(OwnerId, CartKey);
            foreignCart = Donkey != null && Donkey.HasCart && Donkey.GetData(OwnerId, CartDataKey) == "foreign";
            int found = 0;
            for (int i = 0; i < MaxFoals; i++)
            {
                foalHandles[i] = Mounts.Find(OwnerId, FoalKeyOf(i));
                found += foalHandles[i] != null ? 1 : 0;
            }
            if (Donkey != null || Cart != null)
            {
                Plugin.Log.LogInfo($"Companion: {OwnerId} found its {(Donkey != null ? "donkey " + Donkey.FullId : "")}{(Donkey != null && Cart != null ? " and " : "")}{(Cart != null ? "cart " + Cart.FullId : "")}{(found > 0 ? $" and {found} foal(s)" : "")}.");
            }
            nextFoalCheck = 0f;
            ApplyAll();
        }

        private void OnRemoved(MountHandle h)
        {
            if (h == Donkey) Donkey = null;
            if (h == Cart) Cart = null;
            for (int i = 0; i < MaxFoals; i++)
            {
                if (h == foalHandles[i]) foalHandles[i] = null;
            }
        }

        private void OnHitched(MountHandle d, MountHandle c)
        {
            bool own = c == Cart;
            if (own)
            {
                Cart = null; // part of the donkey now
            }
            if (d == Donkey)
            {
                foreignCart = !own;
                Donkey.SetData(OwnerId, CartDataKey, own ? "own" : "foreign");
                ApplyLook();
            }
        }

        private void OnUnhitched(MountHandle d, MountHandle c)
        {
            if (d != Donkey)
            {
                return;
            }
            if (foreignCart)
            {
                // Not this companion's cart: it stands on as its own, with its own look and no key of ours.
                foreignCart = false;
                Donkey.SetData(OwnerId, CartDataKey, "own");
                ApplyLook();
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
            ApplyFoals();
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
                Donkey.SetDisplayName(OwnerId, Current.Name);
            }
        }

        private void ApplyRide()
        {
            if (!Mounts.IsAvailable)
            {
                return;
            }
            foreach (MountStyle style in new[] { MountStyle.Ride, MountStyle.Cart })
            {
                MountTuning t = Mounts.Tuning(style);
                Session(t, "BrayOnMountChance", !brays, 0f);
                Session(t, "BrayIdleEverySeconds", !brays, 0f);
                Session(t, "VoicesVolume", !Mathf.Approximately(Current.SoundVolume, 1f), Current.SoundVolume);
                Session(t, "StepsVolume", !Mathf.Approximately(Current.StepsVolume, 1f), Current.StepsVolume);
                Session(t, "BabyStepsVolume", !Mathf.Approximately(Current.BabyStepsVolume, 1f), Current.BabyStepsVolume);
            }
            MountTuning cart = Mounts.Tuning(MountStyle.Cart);
            Session(cart, "BellOnSetOff", !bell, false);
            Session(cart, "LanternShow", !Current.Lantern, false);
            Session(cart, "LanternLightDayDim", !Current.LanternByDay, 0f);
        }

        // Each foal's own ShuffleFollowKeeperIndoors and ShuffleFollowKeeperOnFoot: set while on, cleared when off if
        // this owner set it.
        private void ApplyFoals()
        {
            if (!Mounts.IsAvailable)
            {
                return;
            }
            foreach (MountHandle h in foalHandles)
            {
                if (h == null || !h.Exists)
                {
                    continue;
                }
                try
                {
                    FoalFlag(h, "ShuffleFollowKeeperIndoors", Current.FoalsComeIndoors);
                    FoalFlag(h, "ShuffleFollowKeeperOnFoot", Current.FoalsFollowOnFoot);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"Companion: the babies' follow switches could not be set on {h.FullId}: {e.Message}");
                }
            }
        }

        private void FoalFlag(MountHandle h, string key, bool on)
        {
            if (on)
            {
                h.Tuning.Set(OwnerId, key, true);
            }
            else if (h.Tuning.IsSet(key) && h.Tuning.SetBy(key) == OwnerId)
            {
                h.Tuning.Clear(OwnerId, key);
            }
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
                    t.Clear(OwnerId, key);
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
            Looks.WarmWanted = skin; // its sheet built ahead: the first spawn wears it at once (Looks.TickWarm)
            MountLook l = Current;
            var coatKeys = Look(CoatPreset(l.Coat, l.CustomCoat, "coat", skin));
            var harnessKeys = Look(HarnessPreset(l.Harness, l.CustomHarness, "harness", skin));
            var muzzleKeys = Look(MuzzlePreset(l.Muzzle, l.CustomMuzzle, "second tone", skin));
            var skinKeys = SkinKeys(skin);
            // the paper: the Mounts panel's choice while it is open (PaperSkin), with that mount's look; else the same
            string ps = PaperSkin;
            bool sameOnPaper = string.Equals(ps, skin, StringComparison.Ordinal);
            MountLook pl = sameOnPaper ? l : LookOf(MountOfSkin(ps));
            var noSkin = new Dictionary<string, object>();
            if (paper != null)
            {
                OnPaper(CoatKeys, sameOnPaper ? coatKeys : Look(CoatPreset(pl.Coat, pl.CustomCoat, "coat", ps)));
                OnPaper(HarnessKeys, sameOnPaper ? harnessKeys : Look(HarnessPreset(pl.Harness, pl.CustomHarness, "harness", ps)));
                OnPaper(MuzzleKeys, sameOnPaper ? muzzleKeys : Look(MuzzlePreset(pl.Muzzle, pl.CustomMuzzle, "second tone", ps)));
                OnPaper(LookKeys, sameOnPaper ? skinKeys : SkinKeys(ps));
                paper.LooksRig.MarkDirty();
            }
            var paperPaint = Look(PaintPreset(pl.Paint, pl.CustomPaint, "cart paint"));
            var paperLantern = Look(LanternPreset(pl.LanternLight, pl.CustomLantern, "lantern"));
            var wheelKeys = Look(WheelPreset(l.Wheels, l.CustomWheels, "wheels"));
            var hubKeys = Look(HubPreset(l.Hubs, l.CustomHubs, "wheel hubs"));
            if (paper != null)
            {
                OnPaper(PaintKeys, paperPaint);
                OnPaper(LanternKeys, paperLantern);
                OnPaper(WheelKeys, sameOnPaper ? wheelKeys : Look(WheelPreset(pl.Wheels, pl.CustomWheels, "wheels")));
                OnPaper(HubKeys, sameOnPaper ? hubKeys : Look(HubPreset(pl.Hubs, pl.CustomHubs, "wheel hubs")));
                paper.ColorsRig.MarkDirty();
            }
            var paintKeys = Look(PaintPreset(l.Paint, l.CustomPaint, "cart paint"));
            var lanternKeys = Look(LanternPreset(l.LanternLight, l.CustomLantern, "lantern"));
            if (Donkey != null && Donkey.Exists)
            {
                Instance(Donkey, CoatKeys, coatKeys);
                Instance(Donkey, HarnessKeys, harnessKeys);
                Instance(Donkey, MuzzleKeys, muzzleKeys);
                Instance(Donkey, LookKeys, skinKeys);
            }
            for (int i = 0; i < foalHandles.Length; i++)
            {
                MountHandle h = foalHandles[i];
                if (h != null && h.Exists)
                {
                    // its own shade of the parent's (FoalVariety.cs)
                    FoalLooks own = LooksOfFoal(i);
                    Instance(h, FoalCoatKeys, Shade(coatKeys, "Coat", own.Hue, own.Brightness, own.Saturation, own.Tinge));
                    Instance(h, HarnessKeys, harnessKeys);
                    Instance(h, FoalMuzzleKeys, Shade(muzzleKeys, "Muzzle", own.MuzzleHue, own.MuzzleBrightness, 1f, 0f));
                    // the dark parts (a mount's wings, the donkey's mane and legs) with the coat: the whole baby its shade
                    Instance(h, FoalDarkKeys, Shade(NoKeys, "Dark", own.Hue * 0.7f, own.Brightness, own.Saturation, 0f));
                    Instance(h, LookKeys, Current.FoalsWearSkin ? skinKeys : noSkin);
                }
            }
            // The cart's colours sit on whichever holds this companion's cart (they move with it at hitch and unhitch);
            // a cart of someone else's that the donkey pulls keeps its own.
            MountHandle withCart = Donkey != null && Donkey.Exists && Donkey.HasCart && !foreignCart ? Donkey : Cart != null && Cart.Exists ? Cart : null;
            if (withCart != null)
            {
                Instance(withCart, PaintKeys, paintKeys);
                Instance(withCart, LanternKeys, lanternKeys);
                Instance(withCart, WheelKeys, wheelKeys);
                Instance(withCart, HubKeys, hubKeys);
            }
        }

        private static Dictionary<string, object> Look(Dictionary<string, object> d) => d ?? new Dictionary<string, object>();

        private static readonly string[] FoalCoatKeys = { "CoatColorize", "CoatColorizeAmount", "CoatBrightness", "CoatSaturation", "CoatHue" };
        private static readonly string[] FoalMuzzleKeys = { "MuzzleColorize", "MuzzleColorizeAmount", "MuzzleBrightness", "MuzzleSaturation", "MuzzleHue" };

        private static readonly string[] FoalDarkKeys = { "DarkHue", "DarkBrightness", "DarkSaturation" };
        private static readonly Dictionary<string, object> NoKeys = new Dictionary<string, object>();

        // A baby's shade of the parent's colours of a region: a colorize colour turned in hue (its hue key would be
        // painted over), else the art's own hue turned; brightness and saturation moved by a step of their own.
        // The step is a visible one on any coat, not a share of it: the Black preset (brightness 0.35, saturation 0.2)
        // took ±15 % of an almost grey charcoal and five babies of one mount looked alike (the user's screenshot, 2026-10-26).
        // So it is at least 0.7 of a full step however dark, and a near-grey coat gets some of the art's own colour
        // back by the baby's tinge (one charcoal, one with a red or wine cast). All 1 and 0: the same as the parent.
        private static Dictionary<string, object> Shade(Dictionary<string, object> parent, string region, float hue, float bri, float sat, float tinge)
        {
            var d = new Dictionary<string, object>(parent);
            if (Math.Abs(hue) < 0.01f && Math.Abs(bri - 1f) < 0.001f && Math.Abs(sat - 1f) < 0.001f && tinge < 0.001f)
            {
                return d;
            }
            if (d.TryGetValue(region + "Colorize", out object c) && c is string hex && hex.Length > 0)
            {
                d[region + "Colorize"] = FoalLooks.ShiftHex(hex, hue, sat);
            }
            else
            {
                d[region + "Hue"] = hue;
            }
            float b = d.TryGetValue(region + "Brightness", out object bv) && bv is float bf ? bf : 1f;
            d[region + "Brightness"] = Mathf.Clamp(b + (bri - 1f) * Mathf.Max(b, 0.7f), 0.05f, 3f);
            float s = d.TryGetValue(region + "Saturation", out object sv) && sv is float sf ? sf : 1f;
            float s2 = s + (sat - 1f) * Mathf.Max(s, 0.7f) + Mathf.Max(0f, 0.5f - s) * 0.6f * tinge;
            d[region + "Saturation"] = Mathf.Clamp(s2, 0f, 3f);
            return d;
        }

        internal static Dictionary<string, object> TestShade(Dictionary<string, object> parent, string region, FoalLooks v)
            => Shade(parent, region, v.Hue, v.Brightness, v.Saturation, v.Tinge);

        private static Dictionary<string, object> SkinKeys(string skinId)
        {
            var d = new Dictionary<string, object>();
            if (!string.IsNullOrWhiteSpace(skinId) && !skinId.Trim().Equals(MountLooks.DonkeyId, StringComparison.OrdinalIgnoreCase)) d["Skin"] = skinId.Trim();
            return d;
        }

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
        private static readonly string[] MuzzleKeys = { "MuzzleColorize", "MuzzleColorizeAmount", "MuzzleBrightness", "MuzzleSaturation" };
        private static readonly string[] PaintKeys =
        {
            "CartBodyColorize", "CartBodyColorizeAmount", "CartBodyBrightness",
            "ShaftsColorize", "ShaftsColorizeAmount", "ShaftsBrightness",
        };
        private static readonly string[] WheelKeys = { "WheelsColorize", "WheelsColorizeAmount", "WheelsBrightness", "WheelsSaturation" };
        private static readonly string[] HubKeys = { "HubsColorize", "HubsColorizeAmount", "HubsBrightness", "HubsSaturation" };
        private static readonly string[] LanternKeys = { "LanternLightColor", "GlowColor", "LitGlassTint" };
        private static readonly string[] LookKeys = { "Skin" };

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
                        h.Tuning.Clear(OwnerId, key);
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"Companion: {key} on {h.FullId} could not be set: {e.Message}");
                }
            }
        }

        // ------------------------------------------------------------------ the presets (the API's, so every player mod looks the same)

        private static Dictionary<string, object> CoatPreset(CoatColor c, string custom, string what, string skinId)
        {
            switch (c)
            {
                case CoatColor.Brown: return new Dictionary<string, object> { ["CoatColorize"] = "8A5A36", ["CoatColorizeAmount"] = 0.85f };
                case CoatColor.Dark: return new Dictionary<string, object> { ["CoatBrightness"] = 0.6f };
                case CoatColor.Cream: return new Dictionary<string, object> { ["CoatColorize"] = "E0CFA8", ["CoatColorizeAmount"] = 0.8f, ["CoatBrightness"] = 1.15f };
                case CoatColor.Black: return new Dictionary<string, object> { ["CoatBrightness"] = 0.35f, ["CoatSaturation"] = 0.2f };
                case CoatColor.Custom:
                    string hex = Hex(custom, what);
                    return hex == null ? null : new Dictionary<string, object> { ["CoatColorize"] = hex, ["CoatBrightness"] = Lightness(hex, "Coat", skinId) };
                default: return null;
            }
        }

        private static Dictionary<string, object> HarnessPreset(HarnessColor c, string custom, string what, string skinId)
        {
            switch (c)
            {
                case HarnessColor.Black: return new Dictionary<string, object> { ["HarnessBrightness"] = 0.35f, ["HarnessSaturation"] = 0.3f };
                case HarnessColor.Red: return new Dictionary<string, object> { ["HarnessColorize"] = "A8322D" };
                case HarnessColor.Blue: return new Dictionary<string, object> { ["HarnessColorize"] = "34558F" };
                case HarnessColor.Green: return new Dictionary<string, object> { ["HarnessColorize"] = "3F6E3A" };
                case HarnessColor.Custom:
                    string hex = Hex(custom, what);
                    return hex == null ? null : new Dictionary<string, object> { ["HarnessColorize"] = hex, ["HarnessBrightness"] = Lightness(hex, "Harness", skinId) };
                default: return null;
            }
        }

        private static Dictionary<string, object> MuzzlePreset(MuzzleColor c, string custom, string what, string skinId)
        {
            switch (c)
            {
                case MuzzleColor.Pale: return new Dictionary<string, object> { ["MuzzleColorize"] = "E8DCC0", ["MuzzleColorizeAmount"] = 1f, ["MuzzleBrightness"] = 1.25f };
                case MuzzleColor.Rose: return new Dictionary<string, object> { ["MuzzleColorize"] = "F0508C", ["MuzzleColorizeAmount"] = 1f, ["MuzzleBrightness"] = 1.3f };
                case MuzzleColor.Brown: return new Dictionary<string, object> { ["MuzzleColorize"] = "6E3A1E", ["MuzzleColorizeAmount"] = 1f, ["MuzzleBrightness"] = 0.75f };
                case MuzzleColor.Green: return new Dictionary<string, object> { ["MuzzleColorize"] = "4E8C3C", ["MuzzleColorizeAmount"] = 1f };
                case MuzzleColor.Blue: return new Dictionary<string, object> { ["MuzzleColorize"] = "3C6EB4", ["MuzzleColorizeAmount"] = 1f };
                case MuzzleColor.Dark: return new Dictionary<string, object> { ["MuzzleBrightness"] = 0.45f, ["MuzzleSaturation"] = 0.6f };
                case MuzzleColor.Custom:
                    string hex = Hex(custom, what);
                    return hex == null ? null : new Dictionary<string, object> { ["MuzzleColorize"] = hex, ["MuzzleBrightness"] = Lightness(hex, "Muzzle", skinId) };
                default: return null;
            }
        }

        private static Dictionary<string, object> PaintPreset(CartPaint c, string custom, string what)
        {
            string tint = null;
            switch (c)
            {
                case CartPaint.DarkWood:
                    return new Dictionary<string, object> { ["CartBodyBrightness"] = 0.6f, ["ShaftsBrightness"] = 0.6f };
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

        // The wheels' wood: as the paint's presets (dark wood, the three tints) plus black.
        private static Dictionary<string, object> WheelPreset(WheelColor c, string custom, string what)
        {
            string tint = null;
            switch (c)
            {
                case WheelColor.DarkWood: return new Dictionary<string, object> { ["WheelsBrightness"] = 0.6f };
                case WheelColor.Black: return new Dictionary<string, object> { ["WheelsBrightness"] = 0.35f, ["WheelsSaturation"] = 0.25f };
                case WheelColor.Red: tint = "9C3B2E"; break;
                case WheelColor.Green: tint = "4C6B3A"; break;
                case WheelColor.Blue: tint = "3E5A86"; break;
                case WheelColor.Custom: tint = Hex(custom, what); break;
            }
            if (tint == null)
            {
                return null;
            }
            var d = new Dictionary<string, object> { ["WheelsColorize"] = tint, ["WheelsColorizeAmount"] = 0.75f };
            if (c == WheelColor.Custom)
            {
                d["WheelsColorizeAmount"] = 1f;
                d["WheelsBrightness"] = Lightness(tint, "Wheels");
            }
            return d;
        }

        // The hubs' iron: blackened, brass or polished.
        private static Dictionary<string, object> HubPreset(HubColor c, string custom, string what)
        {
            switch (c)
            {
                case HubColor.Black: return new Dictionary<string, object> { ["HubsBrightness"] = 0.45f, ["HubsSaturation"] = 0.3f };
                case HubColor.Brass: return new Dictionary<string, object> { ["HubsColorize"] = "B8913A", ["HubsBrightness"] = 1.2f };
                case HubColor.Silver: return new Dictionary<string, object> { ["HubsColorize"] = "B4BCC4", ["HubsBrightness"] = 1.3f };
                case HubColor.Custom:
                    string hex = Hex(custom, what);
                    return hex == null ? null : new Dictionary<string, object> { ["HubsColorize"] = hex, ["HubsBrightness"] = Lightness(hex, "Hubs") };
                default: return null;
            }
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
        // For a mount region the anchor is the worn look's own art when that skin is loaded (docs/mount-colours.md
        // 2.4); a skin still building anchors to the donkey's, and ApplyLook runs again when its sheet is whole
        // (Looks.LoadedReady, subscribed in the constructor).
        private static float Lightness(string hex, string region, string skinId = null)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out Color c);
            Color.RGBToHSV(c, out _, out _, out float v);
            float art = Mathf.Max(0.05f, Colors.ArtLightness(region, skinId));
            return Mathf.Clamp(v / art, 0.15f, 3f);
        }

        private static readonly HashSet<string> warned = new HashSet<string>(StringComparer.Ordinal);

        // RRGGBB, upper case, or null. A bad value is logged once; an empty one is not: it means no colour given yet. Each
        // setter applies the look at once, so a mod setting Coat = Custom and then CustomCoat passes through Custom with
        // no colour for a moment (a mod at every start: three false warnings, 2026-10-03).
        private static string Hex(string value, string what)
        {
            string v = (value ?? "").Trim().TrimStart('#');
            if (v.Length == 0)
            {
                return null;
            }
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
