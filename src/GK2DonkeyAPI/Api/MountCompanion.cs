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

    // The look of one skin, as the colour rows set it (MountCompanion.LookOf; docs/per-skin-looks.md): the coat and
    // harness as drawn with that skin, the cart's paint, wheels, hubs and lantern light with it, and whether the
    // lantern shows. Per skin since 0.41.0 (the user, 2026-10-28: a skin names its own regions, so a red coat with a
    // green second tone is one skin's look and nonsense on another); the mount's own (its name, babies and
    // volumes) is MountOwn. Setting a value applies it at once when this is the worn skin's look (or the Mounts
    // panel's preview's); otherwise it waits for the skin to be worn.
    public sealed class MountLook
    {
        private readonly MountCompanion owner;
        public string SkinId { get; }
        public string MountId => MountCompanion.MountOfSkin(SkinId); // the skin's mount (the donkey for the game's own art)

        internal MountLook(MountCompanion owner, string skinId)
        {
            this.owner = owner;
            SkinId = skinId;
            Dark = new MountTone(this, "dark");
            Eye = new MountTone(this, "eye");
            Outline = new MountTone(this, "outline");
        }

        // The mount's dark parts and its eyes (its Dark and Eye regions), where its look names them (their rows:
        // MountSettingsPage.ToneRows; docs/second-tone.md 8): coloured as a skin's own part is (ToneColor), the whole region.
        public MountTone Dark { get; }
        public MountTone Eye { get; }
        // The line drawn round the mount (its Outline region; the user, 2026-11-04: "set its color by itself as part of
        // an individual option"; docs/mount-colours.md 7.15), where its look has one: Game default is as drawn.
        public MountTone Outline { get; }

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

        // The lantern on the cart's pole, and whether it stays lit by day: part of the look (the user, 2026-10-28: "lantern
        // toggles are part of the customization"). A skin nobody set them for takes the companion's last values.
        internal bool lantern = true, lanternByDay = true;
        public bool Lantern { get => lantern; set { lantern = value; owner.LookChanged(this); } }
        public bool LanternByDay { get => lanternByDay; set { lanternByDay = value; owner.LookChanged(this); } }

        // The colour of one of the skin's own tones (MountSkinInfo.Tones; docs/mount-colours.md section 7), by its id:
        // made on first ask (Game, as drawn). A tone the skin doesn't have keeps its value and changes nothing.
        private readonly Dictionary<string, MountTone> tones = new Dictionary<string, MountTone>(StringComparer.Ordinal);
        public MountTone Tone(string toneId)
        {
            string id = ToneKey(toneId);
            if (!tones.TryGetValue(id, out MountTone t)) tones[id] = t = new MountTone(this, id);
            return t;
        }
        internal MountTone FindTone(string toneId) => tones.TryGetValue(ToneKey(toneId), out MountTone t) ? t : null;
        private static string ToneKey(string toneId) => SkinTones.NormalId(toneId); // as the loader keeps a tone's id
        internal void Changed() => owner.LookChanged(this);
    }

    // A mount's own settings beside its looks (the user, 2026-10-26: the babies, the name and the voice kept per mount,
    // set in the Mounts panel's journal; MountCompanion.OwnOf): set at once when this is the current mount's. A mount
    // nobody set them for takes the companion's last values (MountCompanion.FoalCount etc.).
    public sealed class MountOwn
    {
        private readonly MountCompanion owner;
        public string MountId { get; }

        internal MountOwn(MountCompanion owner, string mountId)
        {
            this.owner = owner;
            MountId = mountId;
        }

        internal int foals;
        internal bool foalsFollow, foalsIndoors, foalsWearSkin = true;
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
                bool trace = Perf.Tracing && StepTrace.On; // dev only: a heavy companion frame in a pick trace split (8.7 ms on the mount's frame, 2026-11-10)
                bool began = trace && StepTrace.Begin("the companion's frame (" + c.OwnerId + ")");
                try
                {
                    c.Tick();
                }
                catch (Exception e)
                {
                    Said.Warn("companion " + c.OwnerId + " " + e.Message, $"Companion of {c.OwnerId}: {e.Message}");
                }
                if (began) StepTrace.End(3.0);
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
        // null when that foal isn't there). Null when none. The current mount's (its Family); with LeaveMountsInWorld
        // the other mounts it had stand where they were left (LeftMounts).
        public MountHandle Donkey { get => cur.Donkey; private set => cur.Donkey = value; }
        public MountHandle Cart { get; private set; }

        // The donkey pulls a cart this companion did not summon (hitched in the world, 2026-10-14): that cart keeps its
        // own look (the paint, wheels, hubs and lantern colours are this companion's own cart's), and when it is left
        // standing again it stays a cart of its own, not this companion's. Kept in the donkey's data for a load.
        private bool foreignCart;
        private const string CartDataKey = "cart";
        // One list for the companion's life, as before the families: it reads the current family's slots live, so a mod that
        // keeps it sees the babies of the mount out after a switch or a load too.
        public IReadOnlyList<MountHandle> Foals { get; }

        private sealed class FoalSlots : IReadOnlyList<MountHandle>
        {
            private readonly MountCompanion c;
            internal FoalSlots(MountCompanion c) => this.c = c;
            public int Count => MaxFoals;
            public MountHandle this[int index] => c.cur.Foals[index];
            public IEnumerator<MountHandle> GetEnumerator()
            {
                for (int i = 0; i < MaxFoals; i++) yield return c.cur.Foals[i];
            }
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }

        // One of this companion's mounts in the world with its babies: the current one (the keys "donkey", "foal" ...) and,
        // with LeaveMountsInWorld, each other mount it had, standing where it was left (the user, 2026-11-05: "leave summoned
        // mounts along with their foals in the game instead of despawning them"; docs/leave-mounts.md), one per mount, under
        // keys of its own (LeftKey). Each baby its own (FoalVariety.cs): its size, shade and temperament from its family's
        // salt (kept in the parent's data, made once, saved with it), the mount it is a baby of and its place among its
        // siblings.
        private sealed class Family
        {
            internal MountHandle Donkey;
            internal readonly MountHandle[] Foals = new MountHandle[MaxFoals];
            internal string MountId; // a left one's mount (its key's); the current one is the skin's
            internal string Salt;
            internal MountHandle SaltOf;
            // Each baby's looks as last worked out, for the salt, the baby mount and the variety they were worked out for: the
            // check once a second made two System.Randoms and a closure per baby every time (0.1-0.3 KB a frame; 2026-11-09).
            internal readonly FoalLooks[] Looks = new FoalLooks[MaxFoals];
            internal readonly bool[] LooksSet = new bool[MaxFoals];
            internal string LooksSalt, LooksMount;
            internal float LooksK = -1f;
        }

        private Family cur = new Family();
        private readonly Dictionary<string, Family> left = new Dictionary<string, Family>(StringComparer.OrdinalIgnoreCase);

        private const string FoalSaltKey = "foalsalt";
        private float varietyApplied = -1f;

        private string FoalSalt(Family f)
        {
            if (f.Donkey == null || !f.Donkey.Exists)
            {
                return "";
            }
            if (!ReferenceEquals(f.SaltOf, f.Donkey) || f.Salt == null)
            {
                f.SaltOf = f.Donkey;
                f.Salt = f.Donkey.GetData(OwnerId, FoalSaltKey);
                if (string.IsNullOrEmpty(f.Salt))
                {
                    f.Salt = Guid.NewGuid().ToString("N").Substring(0, 12);
                    f.Donkey.SetData(OwnerId, FoalSaltKey, f.Salt);
                }
            }
            return f.Salt;
        }

        private static float VarietyNow => IdleShuffle.FoalVariety != null ? IdleShuffle.FoalVariety.Value : 1f;

        // The mount a baby is a baby of: the parent's own when they wear its skin (and it has babies), else the donkey.
        private static string BabyMountId(Family f, MountOwn own)
        {
            GK2DonkeyAPI.Catalog.Entry m = f.Donkey != null && f.Donkey.Exists && own.FoalsWearSkin ? GK2DonkeyAPI.Catalog.Find(f.Donkey.Mount) : null; // the entry itself: the public Find makes a MountInfo each call
            return m != null && m.Foals ? m.Id : MountCatalog.DonkeyId;
        }

        private FoalLooks LooksOfFoal(Family f, MountOwn own, int i)
        {
            string salt = FoalSalt(f), mount = BabyMountId(f, own);
            float k = VarietyNow;
            if (!string.Equals(salt, f.LooksSalt, StringComparison.Ordinal) || !string.Equals(mount, f.LooksMount, StringComparison.Ordinal) || Math.Abs(k - f.LooksK) > 0.0001f)
            {
                f.LooksSalt = salt;
                f.LooksMount = mount;
                f.LooksK = k;
                Array.Clear(f.LooksSet, 0, f.LooksSet.Length);
            }
            if (!f.LooksSet[i])
            {
                f.Looks[i] = FoalLooks.For(salt, mount, i, k);
                f.LooksSet[i] = true;
            }
            return f.Looks[i];
        }

        internal FoalLooks TestLooksOfFoal(int i) => LooksOfFoal(cur, CurrentOwn, i); // the self-test
        // The self-tests of the mounts left in the world (Dev/LeaveMountsTests.cs): a load's finding again by the keys, and a
        // skin taken as a cfg gives it before the game is ready (no switch).
        internal void TestFindAgain() => FindFamilies(reconcile: true);
        internal void TestSkinQuietly(string value) => skin = value ?? "";

        private MountCompanion(string ownerId)
        {
            OwnerId = ownerId;
            Foals = new FoalSlots(this);
            Mounts.GameReady += OnGameReady;
            Mounts.InstanceRemoved += OnRemoved;
            Looks.LoadedReady += id =>
            {
                // the skin's own colours are known now: the custom colours' lightness and the tones' anchor to them, the
                // worn skin's and the Mounts panel picture's (a skin previewed while its sheet built)
                // (each by the look worn for it: a skin not installed wears its mount's own, docs/missing-skin.md)
                bool worn = !string.IsNullOrEmpty(skin) && SameSkin(id, Worn(skin));
                if (worn || (paper != null && SameSkin(id, Worn(PaperSkin)))) ApplyLook();
                foreach (Family f in LeftNow()) if (SameSkin(id, Worn(WornSkin(f.Donkey)))) ApplyLeftLook(f); // a mount left wearing it
            };
            Mounts.Hitched += OnHitched;
            Mounts.Unhitched += OnUnhitched;
            if (Mounts.IsGameReady)
            {
                // made in the middle of a game (a mod making its companion late): its mounts found, none switched, and its
                // mod's first skin taken in place, as before the left mounts (the mount out wears the companion's first
                // skin until then, as it always did)
                FindFamilies(reconcile: false);
                firstSkinInPlace = true;
            }
        }
        private bool firstSkinInPlace;

        // ------------------------------------------------------------------ settings

        private bool keep = true, brays = true, bell, lantern = true, lanternByDay = true, foalsIndoors, foalsFollow;
        private int foals;
        private string name;
        private float volume = 1f;
        // The look (the colour rows) per skin (docs/per-skin-looks.md; per mount from 2026-10-18 to 0.40.0): a mount's
        // green is its default skin's, a fiery skin of the donkey keeps its own reds. Coat, Harness... below are the
        // worn skin's (Current): what a page's rows set is the look of the skin they were built for
        // (MountSettingsPage.PerSkin). The mount's own (the name, babies, volumes) per mount in owns (OwnOf).
        private readonly Dictionary<string, MountLook> looks = new Dictionary<string, MountLook>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, MountOwn> owns = new Dictionary<string, MountOwn>(StringComparer.OrdinalIgnoreCase);

        // The look of a skin by id (made empty, the game's own colours, the first time it is asked for). The donkey's own
        // art is the skin "donkey" (MountLooks.DonkeyId; "" means it too). A mount id (a caller from before 0.41.0, when
        // looks were per mount) gives that mount's first skin's look, said once in the log; an id nothing provides
        // (a pack not installed) is kept as it is, so its look waits for the pack.
        public MountLook LookOf(string skinId)
        {
            string id = string.IsNullOrWhiteSpace(skinId) ? MountLooks.DonkeyId : skinId.Trim();
            if (!id.Equals(MountLooks.DonkeyId, StringComparison.OrdinalIgnoreCase) && Mounts.Looks.Find(id) == null && GK2DonkeyAPI.Catalog.Find(id) != null)
            {
                string first = Looks.FirstSkinOf(id);
                if (mountIdLooks.Add(id)) Plugin.Log.LogInfo($"Companion: {OwnerId}: LookOf(\"{id}\") names a mount; looks are per skin since 0.41.0: its first skin's ({first}) is given.");
                id = first;
            }
            if (!looks.TryGetValue(id, out MountLook l))
            {
                looks[id] = l = new MountLook(this, id) { lantern = lantern, lanternByDay = lanternByDay };
            }
            return l;
        }
        private readonly HashSet<string> mountIdLooks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // A mount's own settings by its id (the companion's last values the first time it is asked for).
        public MountOwn OwnOf(string mountId)
        {
            string id = string.IsNullOrWhiteSpace(mountId) ? MountCatalog.DonkeyId : mountId.Trim();
            if (!owns.TryGetValue(id, out MountOwn o))
            {
                owns[id] = o = new MountOwn(this, id)
                {
                    foals = foals, foalsFollow = foalsFollow, foalsIndoors = foalsIndoors, foalsWearSkin = foalsWearSkin,
                    name = name, volume = volume,
                };
            }
            return o;
        }

        // The mount the companion's skin is a look of (the donkey without a skin, or with one whose mount isn't installed;
        // a skin not installed of a mount that is: that mount, wearing its own look, docs/missing-skin.md).
        public string MountId => MountOfSkin(skin);

        internal static string MountOfSkin(string skinId)
        {
            string s = (skinId ?? "").Trim();
            if (s.Length == 0 || s.Equals(MountLooks.DonkeyId, StringComparison.OrdinalIgnoreCase)) return MountCatalog.DonkeyId;
            return Mounts.Looks.Find(Worn(s))?.Mount ?? MountCatalog.DonkeyId;
        }

        // The look worn for a skin id: itself, or for a skin not installed of a mount that is, that mount's own default
        // (Looks.StandIn, docs/missing-skin.md). The saved id stays the companion's and its mounts'; what is drawn, whose
        // colours apply and which rows show follow this.
        private static string Worn(string skinId) => GK2DonkeyAPI.Looks.StandIn(skinId);

        private MountLook Current => LookOf(Worn(skin));       // the worn skin's look (a stand-in's own, docs/missing-skin.md)
        private MountOwn CurrentOwn => OwnOf(MountId);   // the current mount's own

        // One of a mount's own settings changed: applied when it is the current mount's.
        internal void OwnChanged(MountOwn o)
        {
            if (string.Equals(o.MountId, MountId, StringComparison.OrdinalIgnoreCase)) ApplyMountOwn();
            else if (left.TryGetValue(o.MountId, out Family f)) ApplyLeftOwn(f); // a mount left in the world: its name and babies follow
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

        // A skin's look changed: applied when it is the worn skin's (its lantern through the ride settings too) or the
        // panel's preview's.
        internal void LookChanged(MountLook l)
        {
            bool worn = SameSkin(l.SkinId, Worn(skin)); // a stand-in's look is the worn one (docs/missing-skin.md)
            if (worn) ApplyRide();
            if (worn || SameSkin(l.SkinId, Worn(PaperSkin))) ApplyLook();
            foreach (Family f in LeftNow()) if (SameSkin(l.SkinId, Worn(WornSkin(f.Donkey)))) ApplyLeftLook(f); // a mount left wearing it
        }
        private static bool SameSkin(string a, string b)
        {
            string x = string.IsNullOrWhiteSpace(a) ? MountLooks.DonkeyId : a.Trim(), y = string.IsNullOrWhiteSpace(b) ? MountLooks.DonkeyId : b.Trim();
            return x.Equals(y, StringComparison.OrdinalIgnoreCase);
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
        public int FoalCount { get => CurrentOwn.FoalCount; set { foals = Mathf.Clamp(value, 0, MaxFoals); CurrentOwn.FoalCount = foals; } }

        // The foals come through doors with the keeper (buildings, the church, dungeons), follow him inside and come back
        // out with him (the Shuffle key ShuffleFollowKeeperIndoors on each foal). Off to start with: they stay outside
        // with the donkey.
        public bool FoalsComeIndoors { get => CurrentOwn.FoalsComeIndoors; set { foalsIndoors = value; CurrentOwn.FoalsComeIndoors = value; } }

        // The foals follow the keeper while he walks on foot outdoors, instead of staying with the mount (the Shuffle key
        // ShuffleFollowKeeperOnFoot on each of this companion's foals, never on another mod's); when he gets on a mount
        // they go back to it (following it when it's theirs). Into buildings only with FoalsComeIndoors. Off to start
        // with (0.36.0); FoalRows' row defaults to it (a mod may switch it on first).
        public bool FoalsFollowOnFoot { get => CurrentOwn.FoalsFollowOnFoot; set { foalsFollow = value; CurrentOwn.FoalsFollowOnFoot = value; } }

        // Shown in the donkey's prompt ("Ride Bella"); empty for none.
        public string Name { get => CurrentOwn.Name; set { name = value; CurrentOwn.Name = value; } }

        // Brays at mounting and while standing.
        public bool Brays { get => brays; set { brays = value; ApplyRide(); } }

        // The bell when the cart sets off with cargo (off to start with).
        public bool BellOnSetOff { get => bell; set { bell = value; ApplyRide(); } }

        // The mount's sounds, times the game's SFX volume: 0 to 1.5.
        public float SoundVolume { get => CurrentOwn.SoundVolume; set { volume = Mathf.Clamp(value, 0f, 1.5f); CurrentOwn.SoundVolume = volume; } }

        // The lantern pole and lamp on the cart.
        public bool Lantern { get => Current.Lantern; set { lantern = value; Current.Lantern = value; } }

        // The lamp is lit by day too (the game's cart lamp is); off: it goes dark by day like the hand lantern.
        public bool LanternByDay { get => Current.LanternByDay; set { lanternByDay = value; Current.LanternByDay = value; } }

        // The worn skin's look (LookOf(Skin)); a page's rows set each skin's own (MountSettingsPage.PerSkin).
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
                TakeSkin(value);
            }
        }

        // The skin becomes his: another mount's is another mount (SwitchFamilies: the one he had left where it stands, or
        // changed into the new one as before), then its own name, voice, lantern and babies (each mount keeps its own).
        private void TakeSkin(string value)
        {
            // the switch first, while skin is still the one out's: what it does on the way (the cart's unhitch applies the
            // look) must keep the one out in its own skin, not dress it in the new one before it is left
            string chosen = value ?? "";
            if (firstSkinInPlace)
            {
                firstSkinInPlace = false; // a companion made in the middle of a game: its mod's first skin is the mount it has
                skin = chosen;
                ApplyMountOwn();
                return;
            }
            if (!SwitchFamilies(chosen, "the mount chosen"))
            {
                // the one out is ridden in the air: it lands first (the dismount asked for that), and the mount chosen is
                // taken once it is down (Tick); until then it stays what it is
                if (deferredSkin == null) Plugin.Log.LogInfo($"Companion: {OwnerId}: another mount chosen while the one out can't be let go ({(Mounts.IsGameReady ? "in the air: it lands first" : "a scene loading")}); then {FamilyMountOf(chosen)} is his.");
                deferredSkin = chosen;
                return;
            }
            if (deferredSkin != null)
            {
                // the waiting choice made now (the page giving it again once the landing let him off, say): its call stays,
                // made by TakeDeferred at the next tick, when no panel holds the controls; another choice drops it
                if (!SameSkin(chosen, deferredSkin)) deferredCall = null;
                deferredSkin = null;
            }
            skin = chosen;
            ApplyMountOwn();
        }

        // A mount chosen while the one out couldn't be got off (flying: Mounts.Dismount lands it first), taken in Tick once
        // it is down; the Mounts panel's call that came with it (Ride, Drive the cart) is made then.
        private string deferredSkin;
        private MountStyle? deferredCall;
        private float nextDeferredTry;

        // A call alone waits too: the panel's Ride or Drive while he flies a mount that isn't his own (a left one ridden by hand),
        // which can't be got off in the air either.
        private void TakeDeferred()
        {
            if (deferredSkin == null && !deferredCall.HasValue || !Mounts.IsGameReady || Mounts.IsMounted && Flight.InAir
                || panelHeld || Time.unscaledTime < nextDeferredTry) // not behind the Mounts panel opened again: it is made when it closes
            {
                return;
            }
            nextDeferredTry = Time.unscaledTime + 0.25f; // a try that waits again (still not down) isn't made every frame
            MountStyle? call = deferredCall;
            deferredCall = null;
            if (deferredSkin != null)
            {
                TakeSkin(deferredSkin);
            }
            if (deferredSkin != null)
            {
                deferredCall = call; // still waiting: the call waits with it
            }
            else if (call.HasValue)
            {
                UseFromPanel(call.Value);
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
            if (!commit && deferredSkin != null)
            {
                // a choice waiting for the landing stays as it is, its call with it (the page gives its Mount row's skin
                // again, the one waiting); the mount out keeps its own look meanwhile
                panelSkin = null;
                ApplyMountOwn();
                return;
            }
            string chosen = commit && panelSkin != null ? panelSkin : skin;
            panelSkin = null;
            TakeSkin(chosen);
        }

        private string PaperSkin => panelHeld && panelSkin != null ? panelSkin : skin;
        public bool FoalsWearSkin { get => CurrentOwn.FoalsWearSkin; set { foalsWearSkin = value; CurrentOwn.FoalsWearSkin = value; } }

        // ------------------------------------------------------------------ the keys

        private MountStyle? pending;
        private float pendingUntil;
        private const float PendingSeconds = 5f;

        // The ride key (Ride) or the cart key (Cart). Ok: done, or under way (the donkey is coming). A refusal is also
        // written to the log, with its reason.
        public MountResult Use(MountStyle style)
        {
            StepTrace.Mark("the key's own handler, before Use"); // dev only (a run begun by KeyActions)
            Perf.TracePick(style == MountStyle.Cart ? "the cart key" : "the ride key"); // dev only: the frames from here by part (a 25.8 ms frame was seen once)
            StepTrace.Mark("the pick trace started");
            MountResult r = UseNow(style);
            StepTrace.Mark(r.Ok ? "Use: done or under way" : "Use: refused");
            if (!r.Ok)
            {
                Plugin.Log.LogInfo($"{OwnerId}: {style} not now: {r.Reason}");
            }
            return r;
        }

        // The ride key's and the cart key's actions (0.45.0): Use(Ride) and Use(Cart). Given to a settings page's key as
        // they are (pressed: me.Ride), they also work in the Mount Journal of this companion's page: there the key does what
        // the journal's Ride or Drive the cart button does (the journal closes, keeping the mount picked, and that mount is
        // called; the user, 2026-11-07). Every other key waits while the journal is open.
        public void Ride() => Use(MountStyle.Ride);
        public void Drive() => Use(MountStyle.Cart);

        // The Mounts panel's Ride and Drive the cart buttons (the user, 2026-10-22: pressed while riding, they got him off,
        // as the keys do for the style he is in). Unlike the keys they never end the ride: already on this companion's
        // mount in that style, he stays on (the mount picked in the panel is already on it, changed in place); in the
        // other style, the keys' switch (the cart left standing, or brought); on someone else's mount, he gets off it
        // and this companion's mount is called in the style.
        internal MountResult UseFromPanel(MountStyle style)
        {
            deferredCall = null; // this call replaces one still waiting (the two waits below set it again; review pass 5)
            if (deferredSkin != null)
            {
                deferredCall = style; // the mount chosen in the air: called once the one ridden is down (TakeDeferred)
                Plugin.Log.LogInfo($"{OwnerId}: {style} from the panel: once the mount ridden has landed.");
                return MountResult.Success;
            }
            if (!Mounts.IsAvailable || !Mounts.IsMounted)
            {
                MountResult r = Use(style);
                if (!r.Ok && style == MountStyle.Cart && rideEndedBySwitch == Time.frameCount)
                {
                    // he was riding the one just left (the switch got him off): the cart refused here (a fight), the mount
                    // chosen still comes to ride, as the old change in place kept him riding (review pass 4)
                    Plugin.Log.LogInfo($"{OwnerId}: Drive the cart refused ({r.Reason}); the mount chosen comes to ride.");
                    return Use(MountStyle.Ride);
                }
                return r;
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
                if (!FamiliesHeld && Flight.InAir && LeftFamilyOf(riding) != null)
                {
                    // flying one of his left in the world: the dismount lands it first, the call is made once down
                    // (TakeDeferred); another mod's mount is refused as before
                    deferredCall = style;
                    Plugin.Log.LogInfo($"{OwnerId}: {style} from the panel: once the mount ridden has landed.");
                    return MountResult.Success;
                }
                return MountResult.Refused("Could not get off the mount ridden now.");
            }
            if (!FamiliesHeld && LeftFamilyOf(riding) != null)
            {
                // one of his left in the world, ridden by hand: his cart it pulls is left standing (the next mount fetches it,
                // as at a switch), then it steps aside, so the one called comes beside it
                Vector3? cartWas = null;
                bool by = StandsByKeeper(riding); // he just got off it: by him (asked before its cart's unhitch rebuilds its view)
                if (riding.HasCart && riding.GetData(OwnerId, CartDataKey) != "foreign")
                {
                    Vector3 drawn = riding.I.CartPoint();
                    if (Mounts.Unhitch(riding) != null) cartWas = drawn; // OnUnhitched: his Cart again
                }
                StepAsideForCalled(riding, cartWas, byKeeper: by);
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
            if (!FamiliesHeld && (Donkey == null || !Donkey.Exists) && left.TryGetValue(FamilyMountOf(skin), out Family waiting) && waiting.Donkey != null && waiting.Donkey.Exists)
            {
                Adopt(waiting, "called"); // the chosen mount stands somewhere, left: that one comes (one of each mount)
                ApplyMountOwn();
            }
            if (style == MountStyle.Cart && Mount.Instance.IsInGame(out PlayerController keeper, out _))
            {
                // Asked before anyone is called: the cart refused here (indoors, in a fight) or by the mount (one that pulls
                // none) leaves the mount and the cart where they are, instead of bringing them in and refusing the cart
                // after (BringCart asks the same; the user, 2026-10-26: "although it cant pull it, it still brings it")
                // (a cart holding more than the mount pulls is not refused here: it is brought over and left unhitched, BringCart)
                string noCart = Mount.Instance.WhyNotCart(keeper) ?? (Donkey != null && Donkey.Exists ? GK2DonkeyAPI.Looks.WhyNotCartByMount(Donkey.I) : null);
                StepTrace.Mark("Use: the cart asked (WhyNotCart, by the mount)");
                if (noCart != null)
                {
                    Mount.Say(noCart);
                    StepTrace.Mark("Use: the refusal said (Mount.Say)");
                    return MountResult.Refused(noCart);
                }
            }
            bool fresh = false;
            if (Donkey == null || !Donkey.Exists)
            {
                // Placed by ComeToKeeper right below.
                Donkey = Mounts.Spawn(OwnerId, new MountSpawn { Position = Vector3.zero, Key = DonkeyKey, Persist = keep });
                if (Donkey == null)
                {
                    return MountResult.Refused("Your mount could not be spawned (see the log).");
                }
                fresh = true;
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
                    return MountResult.Refused(UiWords.SayNotInGame.English);
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
                    string notLeft = UiWords.SayCartNotLeftWhereStands.English;
                    Mount.Say(notLeft);
                    return MountResult.Refused(notLeft);
                }
            }
            MountResult come = Donkey.ComeToKeeper();
            if (!come.Ok)
            {
                if (fresh && !FamiliesHeld && (LeaveMountsInWorld || left.Count > 0)) // off with none left: kept as before
                {
                    // spawned for this call at the scene's origin, to be placed by the come: refused here (indoors, busy), it
                    // goes again rather than stand there (review pass 5: with the mounts left, every new mount's first call
                    // spawns one; before, only the first call ever)
                    Plugin.Log.LogInfo($"Companion: {OwnerId}: {Donkey.FullId}, spawned for the call, goes again: {come.Reason}");
                    Donkey.Remove();
                    Donkey = null;
                }
                return come;
            }
            if (!FamiliesHeld)
            {
                // a mount of his left where he stands (he just got off one he rode by hand, say, or walked up to it): it steps
                // aside, so the one called stands on a spot of its own (its body is one to keep clear of now)
                foreach (Family f in LeftNow())
                {
                    MountHandle h = f.Donkey;
                    if (h == null || !h.Exists || h.IsRidden) continue;
                    // his cart on it (he drove it by hand): left standing first, as the panel's call does (UseFromPanel), so
                    // it can step; only one by him (review pass 7: it never stepped, and the one called came into it)
                    Vector3? cartWas = null;
                    bool by = StandsByKeeper(h);
                    if (by && h.HasCart && h.GetData(OwnerId, CartDataKey) != "foreign")
                    {
                        Vector3 drawn = h.I.CartPoint();
                        if (Mounts.Unhitch(h) != null) cartWas = drawn; // OnUnhitched: his Cart again
                    }
                    if (by) StepAsideForCalled(h, cartWas, byKeeper: true);
                }
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
                MountHandle standing = Mounts.UnhitchRidden(out string notLeft);
                if (standing == null)
                {
                    notLeft = string.IsNullOrEmpty(notLeft) ? UiWords.SayCartNotLeftHere.English : notLeft;
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
                StepTrace.Mark("BringCart: the cart asked");
                if (no != null)
                {
                    Mount.Say(no);
                    StepTrace.Mark("BringCart: the refusal said (Mount.Say)");
                    return no;
                }
            }
            // only for a mount of his: on another mod's, his cart would be that mod's once left again (an unhitched cart is
            // its donkey's owner's), so another mod's mount gets a cart of its own as before (review pass 5)
            bool forHis = to != null && (to == Donkey || LeftFamilyOf(to) != null);
            if (!FamiliesHeld && forHis && (Cart == null || !Cart.Exists))
            {
                // his cart on another of his mounts (one left in the world, or his own standing hitched while he rides one
                // left): left standing there first (OnUnhitched makes it Cart), then brought, never a second cart
                var holders = new List<MountHandle> { Donkey };
                foreach (Family f in LeftNow()) holders.Add(f.Donkey);
                foreach (MountHandle h in holders)
                {
                    if (h == null || !h.Exists || !h.HasCart || h.IsRidden || h == to) continue;
                    if (h == Donkey ? foreignCart : h.GetData(OwnerId, CartDataKey) == "foreign") continue;
                    if (Mounts.Unhitch(h) != null && Cart != null && Cart.Exists)
                    {
                        Plugin.Log.LogInfo($"Companion: {OwnerId}: the cart is fetched from {h.FullId} at {h.Position:F2}.");
                        break;
                    }
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
            StepTrace.Mark("BringCart: the cart found or spawned");
            MountResult come = Cart.ComeToKeeper(to.FacingRight);
            StepTrace.Mark("BringCart: the cart came (ComeToKeeper)");
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
            StepTrace.Mark("BringCart: hitched (Mounts.Hitch)");
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
            deferredCall = null; // a call waiting for a landing is called off with it (a waiting choice stays: the page has it)
            if (Donkey != null && Donkey.IsRidden)
            {
                Mounts.Dismount("sent away");
            }
            var all = new List<MountHandle>(cur.Foals) { Cart, Donkey };
            foreach (MountHandle h in all)
            {
                if (h != null && h.Exists)
                {
                    h.SetDownCargo();
                    h.Remove();
                }
            }
            Donkey = Cart = null;
            Array.Clear(cur.Foals, 0, cur.Foals.Length);
            Plugin.Log.LogInfo($"Companion: {OwnerId} sent its donkey away.");
        }

        // ------------------------------------------------------------------ the mounts left in the world

        // Calling another mount (Skin, the Mounts panel's Ride and Drive the cart) leaves the one he had where it stands,
        // with its babies, kept with the save as his mount is (KeepWithSaves); his cart is unhitched and stands beside it,
        // for the next mount to fetch, and another mod's cart it pulls stays with it. Calling that mount again takes the
        // one left back: it comes as a mount called comes. Each mount once at most: a mount left keeps its name, its
        // babies' number and its colours as the journal sets them, and the skin it was left in until it is called again
        // (docs/leave-mounts.md). Off (to start with): the mount he has changes into the one called, where it stands, as
        // before, and no mount stays left: switched off, or a save loaded with it off, the mounts left go home with their
        // babies (a cart one pulls left standing), one he rides by hand once he is off it; that one, called while he
        // rides it, is his again and the one he had goes.
        public bool LeaveMountsInWorld { get; set; }

        // The mounts left in the world, one per mount (their babies are each one's Children), in no set order. Made again only
        // when they change (an options row's greying reads it twice a second while shown): a list a caller holds stays as it was.
        public IReadOnlyList<MountHandle> LeftMounts
        {
            get
            {
                if (leftList == null)
                {
                    var l = new List<MountHandle>();
                    foreach (Family f in left.Values)
                    {
                        if (f.Donkey != null && f.Donkey.Exists) l.Add(f.Donkey);
                    }
                    leftList = l.AsReadOnly(); // a caller can't change the companion's own
                }
                return leftList;
            }
        }
        private IReadOnlyList<MountHandle> leftList; // LeftMounts; null when the left ones changed (LeftChanged)
        private void LeftChanged() => leftList = null;

        // The mounts left in the world go home with their babies (a player mod's button for it). A cart one of
        // them pulls is left standing first (his own is his Cart again, another mod's stands on as its own), so no cart and
        // no load is lost; one being ridden is got off first (one ridden in the air stays: the dismount lands it first).
        // Returns how many went.
        public int SendLeftAway()
        {
            int n = 0;
            foreach (Family f in LeftNow())
            {
                bool was = f.Donkey != null && f.Donkey.Exists;
                if (GoHome(f, "sent home") && was) n++;
            }
            Plugin.Log.LogInfo($"Companion: {OwnerId}: {n} mount(s) left in the world sent home.");
            return n;
        }

        // Off, no mount stays left in the world (the user, 2026-11-05: "switching the option from the options should
        // unsummon the other mounts you're not using right now"): switched off, or a save loaded with it off (FindFamilies
        // found them), the mounts left go home as SendLeftAway sends them; one he rides by hand stays till he is off it.
        // Not during an F11 run (the player's companion is held to the old way) nor behind the Mounts panel. Four times a
        // second, from Tick; nothing to do costs one compare.
        private float nextLeftOffCheck;

        private void SendLeftHomeWhileOff()
        {
            if (LeaveMountsInWorld || left.Count == 0 || Time.unscaledTime < nextLeftOffCheck || FamiliesHeld || panelHeld
                || !Mounts.IsAvailable || !Mounts.IsGameReady)
            {
                return;
            }
            nextLeftOffCheck = Time.unscaledTime + 0.25f;
            int n = 0;
            foreach (Family f in LeftNow())
            {
                MountHandle d = f.Donkey;
                bool was = d != null && d.Exists;
                if (was && d.IsRidden)
                {
                    continue; // ridden by hand: it goes once he is off it
                }
                if (GoHome(f, "the option is off") && was) n++;
            }
            if (n > 0)
            {
                Plugin.Log.LogInfo($"Companion: {OwnerId}: {n} mount(s) left in the world sent home: leaving mounts in the world is off.");
            }
        }

        // Their keys: "left:<mount id>" and its babies' "left:<mount id>#foal", "#foal2" ... (found again after a load).
        internal const string LeftPrefix = "left:";
        internal static string LeftKey(string mountId) => LeftPrefix + mountId;
        internal static string LeftFoalKey(string mountId, int i) => LeftKey(mountId) + "#" + FoalKeyOf(i);

        // The mount a skin is a look of, for one of each mount in the world: the skin's mount; for a skin not installed now,
        // or one whose mount isn't (a third party's skin of a pack's mount, the pack away: its def falls back to the
        // donkey, review pass 12), the mount part of its id ("<mount>/<skin>"), so a mount left stays that mount while its
        // pack is away; else the donkey.
        internal static string FamilyMountOf(string skinId)
        {
            string s = (skinId ?? "").Trim();
            if (s.Length == 0 || s.Equals(MountLooks.DonkeyId, StringComparison.OrdinalIgnoreCase)) return MountCatalog.DonkeyId;
            int slash = s.IndexOf('/');
            string named = slash > 0 ? s.Substring(0, slash) : null;
            // (a short mount part from before the prefixes, "crab/x", is a registered mount's, acme.crab: Looks.Find heals it below)
            if (named != null && GK2DonkeyAPI.Catalog.Find(named) == null && GK2DonkeyAPI.Catalog.FindShort(named) == null)
            {
                return named; // its mount isn't registered now: that mount still, not the donkey a def falls back to
            }
            string m = Mounts.Looks.Find(s)?.Mount;
            if (!string.IsNullOrEmpty(m)) return m;
            if (named == null) return MountCatalog.DonkeyId;
            return (GK2DonkeyAPI.Catalog.Find(named) ?? GK2DonkeyAPI.Catalog.FindShort(named))?.Id ?? named; // the mount's own id, its skin away
        }

        // The skin one of its mounts wears (its own Skin value, ApplyLook's); "" for the donkey's own art.
        private static string WornSkin(MountHandle h)
        {
            if (h == null || !h.Exists || !h.Tuning.IsSet("Skin")) return "";
            return h.Tuning.Get<string>("Skin") ?? "";
        }

        // The left ones in a list of their own: a loop over them may take one back or lose one.
        private List<Family> LeftNow() => new List<Family>(left.Values);

        private Family LeftFamilyOf(MountHandle h)
        {
            if (h == null) return null;
            foreach (Family f in left.Values)
            {
                if (f.Donkey == h) return f;
            }
            return null;
        }

        // The mount changed (TakeSkin; OnGameReady for a save whose mount out is another than the one chosen): the family out
        // is another mount's than the skin's. With LeaveMountsInWorld it is left where it stands (LetGo); the skin's mount
        // left earlier becomes his again (Adopt; it comes when called), else a new one comes at the next call. Off: one
        // left earlier is taken back and the one out goes home; with none left, the one out changes into the new mount where
        // it stands, as before (ApplyLook, after this). At a load (atLoad) none goes home: the one out is left instead, so no
        // mount of his goes without his doing. Before the game is ready nothing is out: OnGameReady finds them.
        // nextSkin: the skin chosen; the field skin is still the one out's while this runs (TakeSkin). False when the one out
        // is ridden and can't be got off now (flying: the dismount lands it first): nothing changed, TakeSkin waits.
        private bool letting; // LetGo under way: ApplyLook doesn't touch the family going
        private int rideEndedBySwitch = -1; // the frame LetGo got him off the mount he rode (UseFromPanel's Drive refused: he rides)
        private enum LetGoResult { Done, Wait, Failed }

        private bool SwitchFamilies(string nextSkin, string why, bool atLoad = false)
        {
            if (!Mounts.IsAvailable)
            {
                return true;
            }
            string next = FamilyMountOf(nextSkin);
            if (!Mounts.IsGameReady)
            {
                // nothing out before a game (OnGameReady finds them); a mount out while a scene loads (a door's frame): another
                // mount chosen then waits for the game to be ready (TakeDeferred), not changed in place with the option on
                bool other = Donkey != null && Donkey.Exists && !string.Equals(cur.MountId ?? FamilyMountOf(WornSkin(Donkey)), next, StringComparison.OrdinalIgnoreCase);
                return !(other && !FamiliesHeld && (LeaveMountsInWorld || left.Count > 0)); // off with none left: in place, as before
            }
            if (FamiliesHeld)
            {
                cur.MountId = next; // a self-test run: changed in place, as before
                return true;
            }
            bool outNow = Donkey != null && Donkey.Exists;
            string now = outNow ? cur.MountId ?? FamilyMountOf(WornSkin(Donkey)) : null;
            if (outNow && string.Equals(now, next, StringComparison.OrdinalIgnoreCase))
            {
                return true; // another skin of the same mount: worn where it stands
            }
            left.TryGetValue(next, out Family waiting);
            if (waiting != null && (waiting.Donkey == null || !waiting.Donkey.Exists))
            {
                left.Remove(next);
                LeftChanged();
                waiting = null;
            }
            if (!outNow)
            {
                if (waiting != null) Adopt(waiting, why);
                else cur.MountId = next;
                return true;
            }
            if (!LeaveMountsInWorld && waiting == null)
            {
                cur.MountId = next; // changes into the new mount where it stands (ApplyLook puts the skin on)
                return true;
            }
            // one of its mount left already (two of a mount: a pack that was away at a load, say): the one out goes home
            // and the one left stays, so one of each mount again
            bool keep = atLoad || LeaveMountsInWorld && !LeftStands(now); // at a load nothing goes home (two of a mount stand till the next switch)
            pending = null;
            bool byKeeper = StandsByKeeper(Donkey); // asked before its cart's unhitch rebuilds its view (the body drawn next frame)
            string gapWas = KeeperGap(Donkey); // for the log, if it doesn't step
            LetGoResult gone;
            Vector3? cartWas;
            letting = true; // ApplyLook (the cart's unhitch calls it) leaves the one going in its own look
            try
            {
                gone = LetGo(now, keep, next, why, out cartWas);
            }
            finally
            {
                letting = false;
            }
            if (gone == LetGoResult.Wait)
            {
                return false;
            }
            if (gone == LetGoResult.Failed)
            {
                cur.MountId = next; // couldn't (the log says why): it changes into the new mount, as before
                if (waiting != null && !atLoad) GoHome(waiting, "a second " + next); // and the one left of it goes: one of each mount (never at a load)
                return true;
            }
            if (keep)
            {
                StepAsideForCalled(cur.Donkey, cartWas, byKeeper, gapWas); // he was on it, or beside it: the one called comes onto a spot of its own
            }
            if (waiting != null)
            {
                Adopt(waiting, why);
            }
            else
            {
                cur = new Family { MountId = next };
                foreignCart = false;
            }
            return true;
        }

        // A self-test run (SelfTest.Drive sets it for the run): every companion's mount changes in place as before the left
        // mounts, no left one taken back or its cart taken off it, so a test's pick and put-back on the player's own
        // companion (the Mounts panel's scenarios) leaves his world as he left it. The left mounts' own scenarios free their
        // test companion (TestFamiliesFree).
        internal static bool HeldForTests;
        internal bool TestFamiliesFree;
        private bool FamiliesHeld => HeldForTests && !TestFamiliesFree;

        private bool LeftStands(string mountId) => mountId != null && left.TryGetValue(mountId, out Family f) && f.Donkey != null && f.Donkey.Exists;

        // A left family goes home: off the keeper, any cart it pulls left standing first (his own is his Cart again, another
        // mod's stands on as its own: no cart and no load lost), then it with its babies. False when it can't now (ridden in
        // the air: the dismount lands it first; its cart wouldn't come off): it stays.
        private bool GoHome(Family f, string why)
        {
            MountHandle d = f.Donkey;
            if (d == null || !d.Exists)
            {
                left.Remove(f.MountId);
                LeftChanged();
                return true;
            }
            if (d.IsRidden && Flight.InAir)
            {
                Plugin.Log.LogInfo($"Companion: {OwnerId}: the {f.MountId} left ({d.FullId}) is ridden in the air; it stays ({why}).");
                return false; // no landing forced on him for it
            }
            if (d.IsRidden)
            {
                Mounts.Dismount("sent home");
                if (d.IsRidden)
                {
                    Plugin.Log.LogInfo($"Companion: {OwnerId}: the {f.MountId} left ({d.FullId}) could not be got off; it stays ({why}).");
                    return false;
                }
            }
            if (d.HasCart && Mounts.Unhitch(d) == null)
            {
                Plugin.Log.LogInfo($"Companion: {OwnerId}: the {f.MountId} left at {d.Position:F2} ({d.FullId}) could not leave its cart; it stays ({why}).");
                return false;
            }
            Plugin.Log.LogInfo($"Companion: {OwnerId}: the {f.MountId} left at {d.Position:F2} ({d.FullId}) went home ({why}).");
            d.Remove(); // its babies go with it; OnRemoved drops the family
            return true;
        }

        // The family out goes from the keys "donkey", "foal" ...: off the keeper (a ride ends where it is), his cart
        // unhitched where it stands (never taken along: the next mount fetches it); then kept under its own keys (keep:
        // another mod's cart it pulls stays with it) or sent home (its babies too; any cart is left standing first). Wait: it
        // is ridden in the air (the dismount lands it first), nothing else changed. Failed: it can't be left or its cart
        // wouldn't come off (the ride may have ended, and its cart be left standing: nothing lost); it changes into the new
        // mount, as before. cartWas: where the cart it left was drawn (StepAsideForCalled keeps clear of it).
        private LetGoResult LetGo(string mountId, bool keep, string next, string why, out Vector3? cartWas)
        {
            MountHandle d = Donkey;
            cartWas = null;
            if (keep && Mount.KeyTaken(OwnerId, LeftKey(mountId)))
            {
                Plugin.Log.LogInfo($"Companion: {OwnerId}: {d.FullId} can't be left as {LeftKey(mountId)} (the key is taken); it changes into {next} where it is.");
                return LetGoResult.Failed; // nothing changed yet
            }
            if (d.IsRidden)
            {
                Mounts.Dismount("another mount called");
                rideEndedBySwitch = Time.frameCount; // the panel's call right after it (UseFromPanel)
                if (d.IsRidden)
                {
                    // flying: the dismount is landing it first (Flight.HoldForLanding); the switch waits for the ground
                    Plugin.Log.LogInfo($"Companion: {OwnerId}: {d.FullId} is ridden in the air; it lands before {next} comes.");
                    return LetGoResult.Wait;
                }
            }
            if (d.HasCart && (!keep || !foreignCart))
            {
                Vector3 drawn = d.I.CartPoint();
                if (Mounts.Unhitch(d) == null)
                {
                    Plugin.Log.LogInfo($"Companion: {OwnerId}: {d.FullId} could not leave its cart; it changes into {next} where it is.");
                    return LetGoResult.Failed;
                }
                cartWas = drawn; // OnUnhitched made his cart Cart again (another mod's stands on its own)
            }
            if (!keep)
            {
                for (int i = 0; i < MaxFoals; i++)
                {
                    MountHandle f = cur.Foals[i];
                    if (f != null && f.Exists) f.Remove();
                }
                Plugin.Log.LogInfo($"Companion: {OwnerId}: {mountId} ({d.FullId}) went home ({why}); {next} is his now.");
                d.Remove();
                return LetGoResult.Done;
            }
            MountResult r = d.SetKey(OwnerId, LeftKey(mountId));
            if (!r.Ok)
            {
                Plugin.Log.LogInfo($"Companion: {OwnerId}: {d.FullId} can't be left as {LeftKey(mountId)} ({r.Reason}); it changes into {next} where it is.");
                return LetGoResult.Failed;
            }
            int babies = 0;
            for (int i = 0; i < MaxFoals; i++)
            {
                MountHandle f = cur.Foals[i];
                if (f == null || !f.Exists) continue;
                babies++;
                if (!f.SetKey(OwnerId, LeftFoalKey(mountId, i)).Ok) Unkey(f, i); // never left holding "foal...": the next mount's babies take those
            }
            cur.MountId = mountId;
            left[mountId] = cur;
            LeftChanged();
            StayWithParent(cur);
            Plugin.Log.LogInfo($"Companion: {OwnerId}: {mountId} ({d.FullId}) left at {d.Position:F2} in {d.SceneId} with {babies} baby(ies){(d.HasCart ? " and another mod's cart" : "")} ({why}); {next} is his now.");
            return LetGoResult.Done;
        }

        // The mount left where the keeper is (he was on it, or by it) steps a little aside (Mount.Instance.StepAsideFrom), so
        // the one called, which comes to him, stands on a spot of its own instead of inside it.
        internal const float CalledMountRoom = 1.6f;

        private static Vector3 KeeperPoint(PlayerController pc)
        {
            Rigidbody rb = pc.PhysicalBody?.Rb;
            return rb != null ? rb.transform.position : pc.PlayerData.position.Value;
        }

        // Its body closer to the keeper than the room a mount called needs (CalledMountRoom, times its drawn width), in his scene.
        private static bool StandsByKeeper(MountHandle h)
        {
            if (h == null || !h.Exists || !Mount.Instance.IsInGame(out PlayerController pc, out _) || h.SceneId != pc.PlayerData.currentGameSceneId)
            {
                return false;
            }
            Vector3 k = KeeperPoint(pc), b = h.I.BodyPoint();
            float room = CalledMountRoom * Mathf.Clamp(h.I.LooksRig.EffectScale, 1f, 2f);
            return new Vector2(b.x - k.x, b.z - k.z).sqrMagnitude < room * room;
        }

        // For the log: what StandsByKeeper measures (its body, drawn or its root without a view yet, and the keeper).
        private static string KeeperGap(MountHandle h)
        {
            if (h == null || !h.Exists || !Mount.Instance.IsInGame(out PlayerController pc, out _) || h.SceneId != pc.PlayerData.currentGameSceneId)
            {
                return "not in his scene";
            }
            Vector3 k = KeeperPoint(pc), b = h.I.BodyPoint();
            float room = CalledMountRoom * Mathf.Clamp(h.I.LooksRig.EffectScale, 1f, 2f);
            return $"its body at {b:F2} ({(h.I.BodyDrawn ? "drawn" : "its root, no body drawn")}), the keeper at {k:F2}: {new Vector2(b.x - k.x, b.z - k.z).magnitude:F2} apart, closer than {room:F2} is by him; {(h.IsRidden ? "ridden" : "standing")}{(h.HasCart ? " with a cart" : "")}";
        }

        // byKeeper: the caller found it by him before it left its cart (StandsByKeeper; the view rebuilt by the unhitch draws
        // its body only next frame), so the distance isn't asked again. gapWas: what that found (KeeperGap), for the log.
        private static void StepAsideForCalled(MountHandle h, Vector3? cartWas, bool byKeeper = false, string gapWas = null)
        {
            // (not with a cart on: another mod's it keeps would be moved with it unchecked, and nothing shuffles a cart)
            // (and once per switch: the panel's switch steps it, then its call of the mount would see the body still drawn on
            // the old spot and step it again, review pass 4)
            if (h == null || !h.Exists || h.I.SteppedAsideJustNow)
            {
                return; // (stepped a moment ago: the panel's call right after its switch, every time)
            }
            string skip = null;
            PlayerController pc = null;
            if (h.HasCart) skip = "a cart on it";
            else if (!Mount.Instance.IsInGame(out pc, out _)) skip = "not in a game";
            else if (h.SceneId != pc.PlayerData.currentGameSceneId) skip = "not in his scene";
            if (skip != null)
            {
                Plugin.Log.LogInfo($"Companion: {h.Owner}: {h.FullId} doesn't step aside for the mount called: {skip}.");
                return;
            }
            Vector3 k = KeeperPoint(pc);
            float scale = Mathf.Clamp(h.I.LooksRig.EffectScale, 1f, 2f); // a mount drawn wider than the donkey steps further
            float room = CalledMountRoom * scale;
            if (!byKeeper && !StandsByKeeper(h))
            {
                // far enough from him already
                Plugin.Log.LogInfo($"Companion: {h.Owner}: {h.FullId} doesn't step aside for the mount called: not by him (now {KeeperGap(h)}{(gapWas != null ? "; before it was left " + gapWas : "")}).");
                return;
            }
            try
            {
                using (Mount.Bind(h.I))
                {
                    h.I.StepAsideFrom(k, room, cartWas, scale);
                }
            }
            catch (Exception e)
            {
                // a look only: the switch around it goes on (it runs while the families change hands)
                Plugin.Log.LogWarning($"Companion: {h.Owner}: {h.FullId} could not step aside: {e.Message}");
            }
        }

        // A mount left earlier becomes his again, under the keys "donkey", "foal" ... (the one out has let them go).
        private void Adopt(Family f, string why)
        {
            MountResult r = f.Donkey.SetKey(OwnerId, DonkeyKey);
            if (!r.Ok)
            {
                Plugin.Log.LogWarning($"Companion: {OwnerId}: the {f.MountId} left in the world ({f.Donkey.FullId}) can't be taken back ({r.Reason}); a new one comes instead.");
                cur = new Family { MountId = f.MountId };
                foreignCart = false;
                return;
            }
            left.Remove(f.MountId);
            LeftChanged();
            for (int i = 0; i < MaxFoals; i++)
            {
                MountHandle h = f.Foals[i];
                if (h != null && h.Exists && !h.SetKey(OwnerId, FoalKeyOf(i)).Ok) Unkey(h, i);
            }
            cur = f;
            foreignCart = f.Donkey.HasCart && f.Donkey.GetData(OwnerId, CartDataKey) == "foreign";
            nextFoalCheck = 0f;
            Plugin.Log.LogInfo($"Companion: {OwnerId}: the {f.MountId} left at {f.Donkey.Position:F2} in {f.Donkey.SceneId} ({f.Donkey.FullId}) is his again ({why}).");
        }

        // A left one's babies stay with it: the follow switches this companion set on them go (FoalFlag).
        private void StayWithParent(Family f)
        {
            foreach (MountHandle h in f.Foals)
            {
                if (h == null || !h.Exists) continue;
                try
                {
                    FoalFlag(h, "ShuffleFollowKeeperIndoors", false);
                    FoalFlag(h, "ShuffleFollowKeeperOnFoot", false);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"Companion: the babies' follow switches could not be cleared on {h.FullId}: {e.Message}");
                }
            }
        }

        // A left one's own settings as the journal has them for its mount: its name and babies (their number at the next
        // check), its look.
        private void ApplyLeftOwn(Family f)
        {
            if (f.Donkey == null || !f.Donkey.Exists)
            {
                return;
            }
            StayWithParent(f);
            if (!Installed(f))
            {
                return; // its mount's pack is away: no journal rows for it, so its name, babies and colours stay as they are
            }
            f.Donkey.SetDisplayName(OwnerId, OwnOf(f.MountId).Name);
            nextFoalCheck = 0f;
            ApplyLeftLook(f);
        }

        // A left one's mount is registered now (its pack installed): only then does the journal have its rows, and its own
        // settings (OwnOf) hold the player's values rather than the companion's first ones.
        private static bool Installed(Family f) => GK2DonkeyAPI.Catalog.Find(f.MountId) != null;

        // Every family gone, the left ones too, their cargo set down (the self-tests' clean-up; SendAway is the current one's).
        internal void SendAwayAll()
        {
            deferredSkin = null; // a choice or call still waiting (a scenario that failed in the air) goes too
            deferredCall = null;
            SendAway();
            foreach (Family f in LeftNow())
            {
                if (f.Donkey != null && f.Donkey.Exists)
                {
                    if (f.Donkey.IsRidden) Mounts.Dismount("sent away");
                    f.Donkey.SetDownCargo();
                    f.Donkey.Remove(); // its babies go with it
                }
            }
            left.Clear();
            LeftChanged();
        }

        // ------------------------------------------------------------------ keeping track

        private float nextFoalCheck;

        private void Tick()
        {
            TakeDeferred();
            StepTrace.Mark("companion: deferred taken");
            SendLeftHomeWhileOff();
            StepTrace.Mark("companion: left mounts sent home");
            if (pending.HasValue)
            {
                ContinuePending();
                StepTrace.Mark("companion: the call went on (ContinuePending)");
            }
            if (Time.unscaledTime >= nextFoalCheck)
            {
                nextFoalCheck = Time.unscaledTime + 1f;
                UpdateFoal();
                StepTrace.Mark("companion: the babies checked (UpdateFoal)");
            }
        }

        private void UpdateFoal()
        {
            if (!Mounts.IsGameReady)
            {
                return;
            }
            float variety = VarietyNow;
            bool newVariety = Math.Abs(variety - varietyApplied) > 0.001f;
            if (UpdateFoals(cur, CurrentOwn, current: true) || newVariety)
            {
                varietyApplied = variety; // a new strength reaches the shades at once (the sizes in UpdateFoals); as before: set when applied
                ApplyLook();
                ApplyFoals();
            }
            foreach (Family f in LeftNow())
            {
                if (!Installed(f)) continue; // its pack away: its babies as they are (ApplyLeftOwn)
                if (UpdateFoals(f, OwnOf(f.MountId), current: false) || newVariety)
                {
                    ApplyLeftLook(f);
                    StayWithParent(f);
                }
            }
        }

        // One family's babies: as many as its mount's own number (MountOwn.FoalCount), each its own size. True when one was
        // born (its look and switches are then set by the caller).
        private bool UpdateFoals(Family fam, MountOwn mountOwn, bool current)
        {
            bool spawned = false;
            MountHandle d = fam.Donkey;
            bool parent = d != null && d.Exists;
            // A baby's size is its mount's (MountDef.FoalScale: the donkey's 0.6, a larger-drawn mount's 0.33): a baby of another
            // size (the mount changed under it) is made again at the right one.
            // ... and only when the babies are the same mount and skin: plain baby donkeys are 0.6 (the row's promise)
            GK2DonkeyAPI.Catalog.Entry babyMount = parent && mountOwn.FoalsWearSkin ? GK2DonkeyAPI.Catalog.Find(d.Mount) : null; // the entry: no MountInfo made each second
            float babySize = babyMount != null && babyMount.Foals ? babyMount.FoalScale : 0.6f; // a mount without babies of its own: plain donkeys
            for (int i = 0; i < MaxFoals; i++)
            {
                MountHandle f = fam.Foals[i];
                bool wanted = i < mountOwn.FoalCount;
                FoalLooks own = wanted ? LooksOfFoal(fam, mountOwn, i) : FoalLooks.Alike;
                float size = babySize * own.Size; // its own size (FoalVariety.cs)
                // (the view's scale is kept in steps of 0.02: 0.33 comes back as 0.32, so a hundredth is no difference;
                // seen 2026-10-18: a baby made again every second)
                if (wanted && f != null && f.Exists && Math.Abs(f.Scale - size) > 0.015f && parent)
                {
                    Plugin.Log.LogInfo($"Companion: {OwnerId}: baby {f.FullId} is {f.Scale:F2} of the grown one, the mount '{d.Mount}' wants {size:F2} for it: its view made again at that size.");
                    f.I.Rescale(size); // the same baby, where it stands (not removed and spawned anew)
                }
                if (wanted && (f == null || !f.Exists) && parent)
                {
                    fam.Foals[i] = Mounts.SpawnFoal(OwnerId, d, size, key: current ? FoalKeyOf(i) : LeftFoalKey(fam.MountId, i), persist: keep);
                    spawned |= fam.Foals[i] != null;
                }
                else if (!wanted && f != null && f.Exists)
                {
                    f.Remove();
                    fam.Foals[i] = null;
                }
                f = fam.Foals[i];
                if (wanted && f != null && f.Exists)
                {
                    f.I.Temper = own.Temper; // not saved: from the seed again (cheap: a few numbers)
                }
            }
            return spawned;
        }

        private void OnGameReady() => FindFamilies(reconcile: true);

        // Its mounts found by their keys (a load; the companion made in a game); reconcile: a save's mount out that isn't the
        // one chosen is switched (a load only: a companion made in the middle of a game gets its skin from its mod next).
        private void FindFamilies(bool reconcile)
        {
            pending = null;
            cur = new Family();
            left.Clear();
            LeftChanged();
            Donkey = Mounts.Find(OwnerId, DonkeyKey);
            Cart = Mounts.Find(OwnerId, CartKey);
            foreignCart = Donkey != null && Donkey.HasCart && Donkey.GetData(OwnerId, CartDataKey) == "foreign";
            int found = 0;
            for (int i = 0; i < MaxFoals; i++)
            {
                cur.Foals[i] = Mounts.Find(OwnerId, FoalKeyOf(i));
                found += cur.Foals[i] != null ? 1 : 0;
            }
            cur.MountId = Donkey != null ? FamilyMountOf(WornSkin(Donkey)) : null; // the mount it was saved as
            if (Donkey != null || Cart != null)
            {
                Plugin.Log.LogInfo($"Companion: {OwnerId} found its {(Donkey != null ? "donkey " + Donkey.FullId : "")}{(Donkey != null && Cart != null ? " and " : "")}{(Cart != null ? "cart " + Cart.FullId : "")}{(found > 0 ? $" and {found} foal(s)" : "")}.");
            }
            // the mounts left in the world (LeaveMountsInWorld), by their keys
            foreach (MountHandle h in Mounts.InstancesOf(OwnerId))
            {
                string k = h.Key;
                if (k == null || !h.HasDonkey || !k.StartsWith(LeftPrefix, StringComparison.Ordinal) || k.IndexOf('#') >= 0 || k.Length == LeftPrefix.Length) continue;
                var f = new Family { Donkey = h, MountId = k.Substring(LeftPrefix.Length) };
                int babies = 0;
                for (int i = 0; i < MaxFoals; i++)
                {
                    f.Foals[i] = Mounts.Find(OwnerId, LeftFoalKey(f.MountId, i));
                    babies += f.Foals[i] != null ? 1 : 0;
                }
                left[f.MountId] = f;
                LeftChanged();
                Plugin.Log.LogInfo($"Companion: {OwnerId} found its {f.MountId} left at {h.Position:F2} in {h.SceneId} ({h.FullId}, {babies} baby(ies)).");
            }
            nextFoalCheck = 0f;
            TakeStrayBabies(cur, current: true);
            foreach (Family f in LeftNow()) TakeStrayBabies(f, current: false);
            if (deferredSkin != null)
            {
                skin = deferredSkin; // a choice waiting for a landing is the cfg's already (the page wrote it): it is his now
            }
            deferredSkin = null;
            deferredCall = null; // its call belonged to the game left
            // the save's mount out isn't the one chosen (a save from before the choice, or the choice changed since): left
            // where it stands, or the one left earlier taken back, as a call of the chosen mount would (SwitchFamilies)
            if (reconcile)
            {
                firstSkinInPlace = false; // a load: the save's mount out is reconciled here
                SwitchFamilies(skin, "the save loaded", atLoad: true);
            }
            ApplyAll();
        }

        // A baby whose slot's key was refused at a switch: no key rather than another family's, and marked with its slot in
        // its data (StraySlotKey), so TakeStrayBabies takes it back at a load, and only it (a mod's own unkeyed baby of
        // the same parent is the mod's, review pass 4).
        private const string StraySlotKey = "strayslot";

        private void Unkey(MountHandle h, int slot)
        {
            h.SetKey(OwnerId, null);
            h.SetData(OwnerId, StraySlotKey, slot.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        // A family's babies that lost their key at a switch (Unkey) are its parent's children still: at a load each takes its
        // own slot back (its size and shade are the slot's), else an empty one, under that slot's key, so the family doesn't
        // get a baby too many.
        private void TakeStrayBabies(Family f, bool current)
        {
            if (f.Donkey == null || !f.Donkey.Exists)
            {
                return;
            }
            foreach (MountHandle c in f.Donkey.Children)
            {
                string was = c.Exists && c.Owner == OwnerId && c.Key == null ? c.GetData(OwnerId, StraySlotKey) : null;
                if (was == null || Array.IndexOf(f.Foals, c) >= 0) continue;
                int slot = int.TryParse(was, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int s) && s >= 0 && s < MaxFoals && f.Foals[s] == null
                    ? s : Array.IndexOf(f.Foals, null);
                if (slot < 0) return;
                f.Foals[slot] = c;
                string key = current ? FoalKeyOf(slot) : LeftFoalKey(f.MountId, slot);
                bool keyed = c.SetKey(OwnerId, key).Ok;
                if (keyed) c.SetData(OwnerId, StraySlotKey, null);
                Plugin.Log.LogInfo($"Companion: {OwnerId}: baby {c.FullId} of {f.Donkey.FullId} had lost its key: it is the baby of slot {slot + 1} again{(keyed ? " (" + key + ")" : ", its key still taken")}.");
            }
        }

        private void OnRemoved(MountHandle h)
        {
            if (h == Donkey) Donkey = null;
            if (h == Cart) Cart = null;
            for (int i = 0; i < MaxFoals; i++)
            {
                if (h == cur.Foals[i]) cur.Foals[i] = null;
            }
            foreach (Family f in LeftNow())
            {
                if (h == f.Donkey)
                {
                    f.Donkey = null;
                    left.Remove(f.MountId); // its babies go with it
                    LeftChanged();
                }
                for (int i = 0; i < MaxFoals; i++)
                {
                    if (h == f.Foals[i]) f.Foals[i] = null;
                }
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
            else if (LeftFamilyOf(d) != null)
            {
                d.SetData(OwnerId, CartDataKey, own ? "own" : "foreign"); // one of his mounts left in the world took it (ridden there): his cart is found on it (BringCart)
            }
        }

        private void OnUnhitched(MountHandle d, MountHandle c)
        {
            if (d != Donkey)
            {
                if (LeftFamilyOf(d) == null)
                {
                    return;
                }
                // one of his mounts left in the world left a cart: his own is his Cart again, another mod's stands on as its own
                bool theirs = d.GetData(OwnerId, CartDataKey) == "foreign";
                d.SetData(OwnerId, CartDataKey, "own");
                if (!theirs && (Cart == null || !Cart.Exists))
                {
                    Cart = c;
                    c.SetKey(OwnerId, CartKey);
                    ApplyLook();
                }
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
            foreach (Family f in LeftNow()) ApplyLeftOwn(f); // the ones left: their names, babies and looks as the journal has them now
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
            foreach (Family f in left.Values)
            {
                if (f.Donkey != null && f.Donkey.Exists) f.Donkey.SetPersist(OwnerId, keep); // a mount left is kept as his own is
            }
        }

        private void ApplyName()
        {
            if (Donkey != null && Donkey.Exists)
            {
                Donkey.SetDisplayName(OwnerId, CurrentOwn.Name);
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
                Session(t, "VoicesVolume", !Mathf.Approximately(CurrentOwn.SoundVolume, 1f), CurrentOwn.SoundVolume);
                Session(t, "StepsVolume", !Mathf.Approximately(CurrentOwn.StepsVolume, 1f), CurrentOwn.StepsVolume);
                Session(t, "BabyStepsVolume", !Mathf.Approximately(CurrentOwn.BabyStepsVolume, 1f), CurrentOwn.BabyStepsVolume);
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
            foreach (MountHandle h in cur.Foals)
            {
                if (h == null || !h.Exists)
                {
                    continue;
                }
                try
                {
                    FoalFlag(h, "ShuffleFollowKeeperIndoors", CurrentOwn.FoalsComeIndoors);
                    FoalFlag(h, "ShuffleFollowKeeperOnFoot", CurrentOwn.FoalsFollowOnFoot);
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
            LookSet k = LookSetOf(l, skin);
            var coatKeys = k.Coat;
            var harnessKeys = k.Harness;
            var muzzleKeys = k.Muzzle;
            var darkKeys = k.Dark;
            var eyeKeys = k.Eye;
            var outlineKeys = k.Outline;
            var skinKeys = k.SkinKeys;
            var tones = k.Tones;
            // the paper: the Mounts panel's choice while it is open (PaperSkin), with that mount's look; else the same
            string ps = PaperSkin;
            string pw = Worn(ps); // the look worn for it (a stand-in's, docs/missing-skin.md); ps stays its Skin
            bool sameOnPaper = string.Equals(ps, skin, StringComparison.Ordinal);
            MountLook pl = sameOnPaper ? l : LookOf(pw); // the previewed skin's own look (per skin since 0.41.0)
            if (paper != null)
            {
                // the previewed skin's own lantern (part of the look, 0.41.0): the paper's own overrides, so the worn skin's
                // session value (ApplyRide) doesn't reach it; by day off: the day dim 0, as ApplyRide does for the ride
                paper.Overrides["LanternShow"] = pl.Lantern;
                paper.Overrides["LanternLightDayDim"] = pl.LanternByDay ? Plugin.Cart.LanternLightDayDim.Entry.Value : 0f;
                paper.OverrideOwners["LanternShow"] = OwnerId;
                paper.OverrideOwners["LanternLightDayDim"] = OwnerId;
                OnPaper(CoatKeys, sameOnPaper ? coatKeys : Look(CoatPreset(pl.Coat, pl.CustomCoat, "coat", pw)));
                OnPaper(HarnessKeys, sameOnPaper ? harnessKeys : Look(HarnessPreset(pl.Harness, pl.CustomHarness, "harness", pw)));
                OnPaper(MuzzleKeys, sameOnPaper ? muzzleKeys : Look(MuzzlePreset(pl.Muzzle, pl.CustomMuzzle, "second tone", pw)));
                OnPaper(DarkKeys, sameOnPaper ? darkKeys : Look(SkinTones.RegionValues(pl.Dark, "Dark", pw)));
                OnPaper(EyeKeys, sameOnPaper ? eyeKeys : Look(SkinTones.RegionValues(pl.Eye, "Eye", pw)));
                OnPaper(OutlineKeys, sameOnPaper ? outlineKeys : Look(SkinTones.RegionValues(pl.Outline, "Outline", pw)));
                OnPaper(LookKeys, sameOnPaper ? skinKeys : SkinKeys(ps));
                SkinTones.Apply(paper, sameOnPaper ? tones : SkinTones.ValuesOf(pl, pw), OwnerId, pw);
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
            if (!letting) // a switch letting the family out go (at a load the skin is already the chosen one): it keeps its own look
            {
                LookOnFamily(cur, skin, l, CurrentOwn, k);
                if (Donkey != null && Donkey.Exists) cur.MountId = FamilyMountOf(skin); // the mount it is now (SwitchFamilies compares the next with it)
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

        // A skin's look as the values its mount's parts take (the presets' keys, the regions', the skin's own tones).
        private sealed class LookSet
        {
            internal Dictionary<string, object> Coat, Harness, Muzzle, Dark, Eye, Outline, SkinKeys;
            internal Dictionary<string, Colors.RegionTweak> Tones;
        }

        // skinId: the skin chosen, kept as the instance's Skin; its parts and tones are the look worn for it (a stand-in's,
        // docs/missing-skin.md)
        private static LookSet LookSetOf(MountLook l, string skinId) => LookSetOf(l, skinId, Worn(skinId));

        private static LookSet LookSetOf(MountLook l, string skinId, string look) => new LookSet
        {
            Coat = Look(CoatPreset(l.Coat, l.CustomCoat, "coat", look)),
            Harness = Look(HarnessPreset(l.Harness, l.CustomHarness, "harness", look)),
            Muzzle = Look(MuzzlePreset(l.Muzzle, l.CustomMuzzle, "second tone", look)),
            // the dark parts and the eyes, where the look names them (their rows: ToneRows), as a skin's own part is coloured
            Dark = Look(SkinTones.RegionValues(l.Dark, "Dark", look)),
            Eye = Look(SkinTones.RegionValues(l.Eye, "Eye", look)),
            Outline = Look(SkinTones.RegionValues(l.Outline, "Outline", look)),
            SkinKeys = SkinKeys(skinId),
            Tones = SkinTones.ValuesOf(l, look), // the skin's own tones (docs/mount-colours.md section 7)
        };

        // A family's donkey and babies in a skin's look (the current one's from ApplyLook, a left one's from ApplyLeftLook):
        // the donkey wears it, each baby its own shade of it (FoalVariety.cs).
        private void LookOnFamily(Family fam, string skinId, MountLook l, MountOwn mountOwn, LookSet k)
        {
            string look = Worn(skinId); // the look worn for the skin chosen (a stand-in's, docs/missing-skin.md)
            MountHandle d = fam.Donkey;
            if (d != null && d.Exists)
            {
                Instance(d, CoatKeys, k.Coat);
                Instance(d, HarnessKeys, k.Harness);
                Instance(d, MuzzleKeys, k.Muzzle);
                Instance(d, DarkKeys, k.Dark);
                Instance(d, EyeKeys, k.Eye);
                Instance(d, OutlineKeys, k.Outline);
                Instance(d, LookKeys, k.SkinKeys);
                SkinTones.Apply(d.I, k.Tones, OwnerId, look);
            }
            // a baby's own shade of the dark parts only where they are a part of the look it wears (named: the donkey's
            // mane, legs and ears); where that look names no Dark, its Dark is the outline, the drawing's line, which no
            // colour reaches (docs/mount-colours.md 7.11)
            bool babyDarkIsPart = SkinRegions.NameTerm(mountOwn.FoalsWearSkin ? look : MountLooks.DonkeyId, "Dark") != null;
            var noSkin = new Dictionary<string, object>();
            for (int i = 0; i < fam.Foals.Length; i++)
            {
                MountHandle h = fam.Foals[i];
                if (h != null && h.Exists)
                {
                    // its own shade of the parent's (FoalVariety.cs)
                    FoalLooks own = LooksOfFoal(fam, mountOwn, i);
                    Instance(h, FoalCoatKeys, Shade(k.Coat, "Coat", own.Hue, own.Brightness, own.Saturation, own.Tinge));
                    Instance(h, HarnessKeys, k.Harness);
                    Instance(h, FoalMuzzleKeys, Shade(k.Muzzle, "Muzzle", own.MuzzleHue, own.MuzzleBrightness, 1f, 0f));
                    // the dark parts (the donkey's mane, legs and ears) with the coat: the whole baby its shade, of the
                    // parent's dark colour when the look gives one (the eyes are the parent's); an outline the parent's
                    Instance(h, FoalDarkKeys, babyDarkIsPart ? Shade(k.Dark, "Dark", own.Hue * 0.7f, own.Brightness, own.Saturation, 0f) : k.Dark);
                    Instance(h, EyeKeys, k.Eye);
                    Instance(h, OutlineKeys, k.Outline); // the line drawn round it: the parent's, no shade of its own
                    Instance(h, LookKeys, mountOwn.FoalsWearSkin ? k.SkinKeys : noSkin);
                    // the parent's tones on a baby in its skin, each given a colour in the baby's own shade (as its coat); one
                    // not in the skin is a plain donkey (no tones of its own)
                    SkinTones.Apply(h.I, mountOwn.FoalsWearSkin && k.Tones != null ? SkinTones.ValuesOf(l, look, own) : null, OwnerId, look);
                }
            }
        }

        // A mount left in the world keeps the skin it was left in (the Skin row of a mount not chosen reaches no one), in
        // that skin's look as the colour rows set it now, and its babies in theirs.
        private void ApplyLeftLook(Family f)
        {
            if (!Mounts.IsAvailable || f.Donkey == null || !f.Donkey.Exists || !Installed(f))
            {
                return;
            }
            string chosen = WornSkin(f.Donkey); // its Skin: the choice, kept
            string worn = Worn(chosen);         // the look it wears for it (a stand-in's, docs/missing-skin.md)
            if (!SameSkin(worn, MountLooks.DonkeyId) && Mounts.Looks.Find(worn) == null)
            {
                return; // its skin's pack is away (its mount's too): its colours stay as they are until the skin's rows are back
            }
            MountLook l = LookOf(worn);
            LookOnFamily(f, chosen, l, OwnOf(f.MountId), LookSetOf(l, chosen));
        }

        private static Dictionary<string, object> Look(Dictionary<string, object> d) => d ?? new Dictionary<string, object>();

        private static readonly string[] FoalCoatKeys = { "CoatColorize", "CoatColorizeAmount", "CoatBrightness", "CoatSaturation", "CoatHue" };
        private static readonly string[] FoalMuzzleKeys = { "MuzzleColorize", "MuzzleColorizeAmount", "MuzzleBrightness", "MuzzleSaturation", "MuzzleHue" };

        private static readonly string[] DarkKeys = { "DarkColorize", "DarkColorizeAmount", "DarkBrightness", "DarkSaturation" };
        private static readonly string[] EyeKeys = { "EyeColorize", "EyeColorizeAmount", "EyeBrightness", "EyeSaturation" };
        private static readonly string[] OutlineKeys = { "OutlineColorize", "OutlineColorizeAmount", "OutlineBrightness", "OutlineSaturation" };
        private static readonly string[] FoalDarkKeys = { "DarkColorize", "DarkColorizeAmount", "DarkHue", "DarkBrightness", "DarkSaturation" };

        // A baby's shade of the parent's colours of a region: a colorize colour turned in hue (its hue key would be
        // painted over), else the art's own hue turned; brightness and saturation moved by a step of their own.
        // The step is a visible one on any coat, not a share of it: the Black preset (brightness 0.35, saturation 0.2)
        // took ±15 % of an almost grey charcoal and five babies of one mount looked alike (the user's screenshot, 2026-10-26).
        // So it is at least 0.7 of a full step however dark, and a near-grey coat gets some of the art's own colour
        // back by the baby's tinge (one charcoal, one with a red or wine cast). All 1 and 0: the same as the parent.
        private static Dictionary<string, object> Shade(Dictionary<string, object> parent, string region, float hue, float bri, float sat, float tinge)
        {
            var d = new Dictionary<string, object>(parent);
            if (FoalLooks.IsAlike(hue, bri, sat, tinge))
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
            d[region + "Brightness"] = FoalLooks.StepBrightness(b, bri);
            float s = d.TryGetValue(region + "Saturation", out object sv) && sv is float sf ? sf : 1f;
            d[region + "Saturation"] = FoalLooks.StepSaturation(s, sat, tinge);
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
        private static float Lightness(string hex, string region, string skinId = null) => LightnessAgainst(hex, Colors.ArtLightness(region, skinId));

        // The rule itself: the colour's value over the art's average value (0..1), for a region and a skin's tone alike
        // (SkinTones.Lightness).
        internal static float LightnessAgainst(string hex, float art)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out Color c);
            Color.RGBToHSV(c, out _, out _, out float v);
            return Mathf.Clamp(v / Mathf.Max(0.05f, art), 0.15f, 3f);
        }

        private static readonly HashSet<string> warned = new HashSet<string>(StringComparer.Ordinal);

        // RRGGBB, upper case, or null. A bad value is logged once; an empty one is not: it means no colour given yet. Each
        // setter applies the look at once, so a mod setting Coat = Custom and then CustomCoat passes through Custom with
        // no colour for a moment (a mod at every start: three false warnings, 2026-10-03). A skin's tones read theirs
        // here too (SkinTones.ValuesOf).
        internal static string Hex(string value, string what)
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
