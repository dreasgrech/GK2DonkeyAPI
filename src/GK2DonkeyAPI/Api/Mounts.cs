using System;
using System.Collections.Generic;
using UnityEngine;

namespace GK2DonkeyAPI.Api
{
    // The two ways to use the mount: sit on the donkey, or ride it while it pulls its cart.
    // Where the ridden mount is, off the ground or on it (Mounts.FlightState, FlightChanged; 0.36.0).
    public enum MountFlight
    {
        Ground,
        Jumping,
        TakingOff,
        Flying,
        Landing,
    }

    public enum MountStyle
    {
        Ride,
        Cart,
    }

    // The Mounts panel's speed words, slowest first (0.40.0): what a mount's speed reads as against the donkey's, and the
    // one speed every mount takes with the Behaviour key FreeplaySameSpeed (FreeplaySpeed). Their words, in this order:
    // Mounts.Catalog.SpeedWords.
    public enum SpeedBand
    {
        VerySlow,
        Slow,
        Normal,
        Fast,
        VeryFast,
    }

    // Parts of a ride another mod can switch off (Mounts.AddFeatureRule). Each also has the core's own settings; a
    // feature is on when those say so and every rule allows it. Riding in Cart style at all is not a feature: refuse
    // it with a mount rule on MountStyle.Cart, which can say why.
    public enum MountFeature
    {
        Cargo,            // the cart carries items: pickups land on it, set down with the interact key
        Lantern,          // the cart's lantern pole and lamp are shown
        LanternLight,     // the lamp is lit: its lights, glow and lit glass
        Sounds,           // every mount sound (hoofs, wheels, brays, graze, bell, cargo thump)
        Bell,             // the bell rings (on setting off, the bell key, Mounts.RingBell)
        StayThroughDoors, // the ride goes on through doors (the donkey is summoned again in the next scene)
    }

    // The answer to TryMount / SwitchStyle: Ok, or the reason it was refused (the text the keeper's bubble shows).
    public readonly struct MountResult
    {
        public bool Ok { get; }
        public string Reason { get; }

        private MountResult(bool ok, string reason)
        {
            Ok = ok;
            Reason = reason;
        }

        public static MountResult Success => new MountResult(true, null);

        public static MountResult Refused(string reason) => new MountResult(false, reason ?? "refused");

        public override string ToString() => Ok ? "ok" : "refused: " + Reason;
    }

    // Public API of GK2 Donkey API for other mods (docs/api-design.md).
    //
    // Reference GK2DonkeyAPI.dll and declare [BepInDependency("dreas.gk2.donkeyapi")]. Call from the Unity main thread only
    // (Update, event handlers, Harmony patches of game code). Everything goes through the core's own safety checks:
    // rules added here can refuse more, never allow what the core refuses (co-op, busy, building, saves; indoors is a
    // Behaviour switch, DismountIndoors and CartIndoors, not a rule).
    public static class Mounts
    {
        // The API's version, the same number as the mod's (Plugin.Version is the one place): minor for additions, major
        // for breaking changes.
        public static readonly Version ApiVersion = new Version(Plugin.Version);

        // False when the core is switched off (General.Enabled) or its start-up check of the game's code stopped it.
        public static bool IsAvailable => Plugin.Patched && Plugin.Enabled != null && Plugin.Enabled.Value;

        // For a mod's status line: "Active", or what the API is doing instead and why: a feature off or on changed code
        // after a game update (its start-up check, GameCode), stopped by that check, or switched off (General.Enabled).
        public static string StatusText => IsAvailable ? GameCode.Status
            : GameCode.Stopped ? GameCode.Status
            : "Off: GK2 Donkey API is off (its General.Enabled; see BepInEx/LogOutput.log).";

        public static bool IsMounted => Mount.IsMounted;

        // The style being ridden; meaningless while not mounted.
        public static MountStyle Style => Mount.CurrentStyle;

        // Id of the mod that started the current ride (null while not mounted).
        public static string Owner => Mount.IsMounted ? owner : null;

        // Summon the donkey under the keeper in this style. Refused (with the reason) when the core or a rule says no,
        // when already mounted, or outside a loaded game. showMessage: say the reason in the keeper's bubble.
        public static MountResult TryMount(string ownerId, MountStyle style, bool showMessage = true)
        {
            if (string.IsNullOrEmpty(ownerId))
            {
                throw new ArgumentException("An owner id (your plugin GUID) is required.", nameof(ownerId));
            }
            if (!IsAvailable)
            {
                return MountResult.Refused("GK2 Donkey API is disabled.");
            }
            if (Mount.IsMounted)
            {
                return MountResult.Refused("Already mounted.");
            }
            owner = ownerId; // before the attempt: Mounted handlers may read Owner
            string reason = Mount.TryMountFromApi(style, showMessage);
            if (reason != null)
            {
                owner = null;
                return MountResult.Refused(reason);
            }
            return MountResult.Success;
        }

        // End the ride (cargo is handed back to the keeper first). Does nothing while not mounted.
        public static void Dismount(string reason = null)
        {
            if (Mount.IsMounted)
            {
                if (Flight.HoldForLanding("dismount: " + (reason ?? "dismounted"))) return; // flying free: down first
                Mount.DismountFromApi(string.IsNullOrEmpty(reason) ? "dismounted" : reason);
            }
        }

        // Mount again in the other style, in place. The rules for the new style are asked first; if one refuses,
        // the ride goes on unchanged and the refusal is returned.
        public static MountResult SwitchStyle(MountStyle style, bool showMessage = true)
        {
            if (!Mount.IsMounted)
            {
                return MountResult.Refused("Not mounted.");
            }
            if (Mount.CurrentStyle == style)
            {
                return MountResult.Success;
            }
            if (style == MountStyle.Cart && Flight.InAir)
            {
                return MountResult.Refused("flying"); // no cart in the air (the flying experiment): quietly, no landing
            }
            if (Flight.HoldForLanding("to " + style))
            {
                return MountResult.Refused("Land first."); // flying free: down first
            }
            string reason = (style == MountStyle.Cart && Mount.Instance.IsInGame(out PlayerController keeper, out _) ? Mount.Instance.WhyNotCart(keeper) ?? GK2DonkeyAPI.Looks.WhyNotCartByMount(Mount.Ridden) : null) ?? MountRules.WhyCannotMount(style);
            if (reason != null)
            {
                if (showMessage)
                {
                    Mount.Say(reason);
                }
                return MountResult.Refused(reason);
            }
            if (!Mount.Ridden.Ephemeral)
            {
                // A donkey standing in the world has its own cart or none: to Ride leaves the cart standing where it is
                // (Unhitch), to Cart needs a cart to hitch (Hitch).
                if (style == MountStyle.Ride)
                {
                    if (UnhitchRidden() != null)
                    {
                        return MountResult.Success;
                    }
                    if (showMessage)
                    {
                        Mount.Say("Not right now.");
                    }
                    return MountResult.Refused("Not right now.");
                }
                string noCart = $"This {Mount.Current?.Noun ?? "mount"} has no cart here; ride it up to one.";
                if (showMessage)
                {
                    Mount.Say(noCart);
                }
                return MountResult.Refused(noCart);
            }
            string keepOwner = owner;
            Mount.DismountFromApi("style change");
            MountResult result = TryMount(keepOwner ?? Plugin.Guid, style, showMessage);
            return result;
        }

        // Both respect the Sounds feature (and RingBell the Bell feature). The bell is the cart's (0.40.0; the user,
        // 2026-10-27): it rings only while the keeper drives the cart, never riding plain (a dragon has no cart).
        public static void RingBell()
        {
            if (Mount.IsMounted && Mount.CurrentStyle == MountStyle.Cart && MountRules.Allowed(MountFeature.Sounds) && MountRules.Allowed(MountFeature.Bell))
            {
                GK2DonkeyAPI.Sounds.RingBell(); // the type, not the Sounds property of this class
            }
        }

        public static void Bray()
        {
            if (Mount.IsMounted && MountRules.Allowed(MountFeature.Sounds))
            {
                GK2DonkeyAPI.Sounds.Bray();
            }
        }

        // A rule asked before every mount: return null to allow, or the reason to refuse (shown in the keeper's
        // bubble). Rules from all mods must allow. Dispose the returned handle to remove the rule.
        public static IDisposable AddMountRule(string ownerId, Func<MountStyle, string> whyNot)
        {
            return MountRules.AddMount(ownerId, whyNot);
        }

        // A rule asked about twice a second while riding: return null to keep riding, or the reason to dismount
        // (shown in the keeper's bubble). Dispose the returned handle to remove the rule.
        public static IDisposable AddStayRule(string ownerId, Func<string> whyDismount)
        {
            return MountRules.AddStay(ownerId, whyDismount);
        }

        // A rule for one part of the ride: return false to switch it off. Asked at mount, about twice a second while
        // riding, and on RefreshRules(). Switching Cargo off mid-ride hands the cargo back to the keeper.
        public static IDisposable AddFeatureRule(string ownerId, MountFeature feature, Func<bool> allowed)
        {
            return MountRules.AddFeature(ownerId, feature, allowed);
        }

        // What the feature rules say right now (the core's own settings are not part of it).
        public static bool IsFeatureAllowed(MountFeature feature)
        {
            return MountRules.Allowed(feature);
        }

        // Ask the feature rules again now instead of within the next half second (e.g. right after your mod
        // unlocked something).
        public static void RefreshRules()
        {
            MountRules.Poll(force: true);
        }

        // The tuning values of a style, to read and to override for this session (see MountTuning).
        public static MountTuning Tuning(MountStyle style)
        {
            if (!Plugin.Patched && Plugin.Ride == null)
            {
                throw new InvalidOperationException("GK2 Donkey API has not loaded its settings yet.");
            }
            if (style == MountStyle.Cart)
            {
                return cartTuning ?? (cartTuning = new MountTuning(MountStyle.Cart, Plugin.Cart));
            }
            return rideTuning ?? (rideTuning = new MountTuning(MountStyle.Ride, Plugin.Ride));
        }

        // The ride behaviour switches, for both styles, overridable like tuning (keys = their [General] cfg names):
        //   StayMountedThroughDoors  keep riding through doors that load another scene
        //   DismountIndoors          dismount entering a building, church or dungeon, and refuse to mount inside
        //   CartIndoors              with DismountIndoors off: the cart comes indoors too (0.36.0, off); off, a cart
        //                            ride through a building's door leaves the cart there and goes on as a ride (at
        //                            the morgue: off inside, the corpse into the hands), the cart refused inside
        //   ShowMessages             the keeper's thought bubble for refusals
        //   CargoZombies             zombies ride on the cart (0.22.0, on); off: the cart refuses them
        //   CargoChest               a plain storage chest rides on the cart, alone (0.23.0, on); off: chests stay put
        //   CorpseIntoMorgue         a corpse on the cart goes into the keeper's hands when the ride ends at the morgue
        //                            door, so he walks in carrying it (0.27.0, on); CorpseIntoMorguePlaces (a string):
        //                            the area ids and door destinations that count ("morgue, tp_RT_morgue_exit, ...")
        //   FlyIndoors               a flying mount may take off and fly indoors, as a lift only (0.36.0, off); off: no
        //                            take-off indoors, and a flight through a building's door lands there
        //   JumpKey, FlyKey          (KeyboardShortcut, 0.36.0, Space both) riding plain: jump; take off and land a mount that
        //                            flies. The same key: a second press in the air during a jump takes off. None: off.
        //                            Read only while FlyingEnabled (on unless the dev tab switched it off); Space is left to
        //                            the game's attack in a fight.
        //   ClimbKey, DescendKey     (KeyboardShortcut, 0.38.0, Left Shift and Left Ctrl) flying: held, climb and descend (at
        //                            the ground, descending has landed). The main key alone counts: a modifier can be the key.
        //   FallGuard                (0.36.0, on) the keeper fallen out of the world (under the ground, into nothing, far
        //                            below it) is put back on the last ground he stood on (docs/never-lost.md)
        //   ReturnKey                (KeyboardShortcut, 0.36.0, None) a key for ReturnToSafeSpot
        //   IdleShuffle              donkeys standing in the world without a cart take a few slow steps now and then
        //                            (off in the core; how they step: Mounts.Shuffle). Per donkey: MountHandle.Tuning.
        //   FreeplayMaxCargo         (0.40.0, off) every mount that pulls a cart carries the most a cart holds (6), whatever
        //                            its own cargo; MountInfo.Cargo and the panel's card say so too
        //   FreeplaySameSpeed        (0.40.0, off) every mount, the donkey too, goes at FreeplaySpeed's speed (a SpeedBand,
        //   FreeplaySpeed            Normal: the donkey's as shipped; with the cart times the donkey's ratio); MountInfo's
        //                            speeds and the panel say so too. Both Saddle Up's Freeplay section.
        public static MountTuning Behaviour
        {
            get
            {
                if (Plugin.ShowMessages == null)
                {
                    throw new InvalidOperationException("GK2 Donkey API has not loaded its settings yet.");
                }
                return behaviour ?? (behaviour = new MountTuning("General", new ITuned[]
                {
                    Plugin.StayMountedThroughDoors, Plugin.DismountIndoors, Plugin.CartIndoors, Plugin.ShowMessages, IdleShuffle.On, Plugin.HitchKey, Plugin.CargoZombies, Plugin.CargoChest,
                    Plugin.CorpseIntoMorgue, Plugin.CorpseIntoMorguePlaces, Plugin.CartSlots, Plugin.FlyIndoors, Plugin.JumpKey, Plugin.FlyKey, Plugin.ClimbKey, Plugin.DescendKey,
                    Plugin.FallGuard, Plugin.ReturnKey, Plugin.StandingSounds, Plugin.FreeplayMaxCargo, Plugin.FreeplaySameSpeed, Plugin.FreeplaySpeed,
                }));
            }
        }

        // How a standing donkey shuffles when IdleShuffle is on, overridable like tuning (keys = their [Shuffle] cfg
        // names): ShuffleAreaWidth / ShuffleAreaDepth (the box around its home spot, either way), ShuffleStepMin /
        // ShuffleStepMax, ShuffleSpeed, ShuffleAnimSpeed, ShufflePauseMin / ShufflePauseMax, ShuffleMaxAngle (steepest
        // step from the screen's horizontal), ShuffleTurnChance, ShuffleKeeperDistance (holds still while the keeper
        // is closer). Its home spot is where it was spawned, left or last moved (MountHandle.MoveTo, ComeToKeeper).
        public static MountTuning Shuffle
        {
            get
            {
                if (IdleShuffle.On == null)
                {
                    throw new InvalidOperationException("GK2 Donkey API has not loaded its settings yet.");
                }
                return shuffle ?? (shuffle = new MountTuning(IdleShuffle.Section, IdleShuffle.Settings));
            }
        }

        // Corpses at the crossroads (docs/corpse-delivery.md), overridable like tuning (keys = their [Corpses] cfg
        // names). Off in the core; a mod turns it on for its player (Saddle Up's "Donkey leaves corpses at the
        // crossroads"), and while it is on the game's own donkey leaves the bodies it brings where it appears, by the
        // nun, instead of putting them on the morgue pallets; the keeper hauls them himself (the cart takes them).
        //   CorpsesAtCrossroads        the switch (off). Off: nothing of the game's delivery is hooked at all.
        //   CorpsesUnloadAtCrossroads  the donkey walks out from behind its tree to CorpsesStandPoint, unloads there and
        //                              walks back (on); off: it walks to the morgue as in the game, and the bodies
        //                              still appear at the drop point when it arrives.
        //   CorpsesStandPoint          GD point it walks to (donkey_01_01, the road beside the nun).
        //   CorpsesDropPoint           GD point the bodies are left at (nun_body_drop_point, the game's own spot by her).
        //   CorpsesNeverRot            the bodies it leaves never turn into a grave mound (off; the game's rule is about
        //                              75 minutes of play out of sight).
        //   CorpsesNotification        text shown instead of the game's "New Body", with the body's icon; "" = the game's.
        //   CorpsesUnloadDelay / CorpsesLeaveDelay  only when it unloads on the spot (the stand point is its spawn point,
        //                              or no walk is possible): seconds before the bodies appear / before it vanishes.
        // The game's own rules stay: deliveries every second day, the body roll, the morgue's capacity (bodies on the
        // ground count), the quests that keep the donkey at the morgue for a talk (then it walks there as in the game).
        public static MountTuning Corpses
        {
            get
            {
                if (GK2DonkeyAPI.Corpses.AtCrossroads == null)
                {
                    throw new InvalidOperationException("GK2 Donkey API has not loaded its settings yet.");
                }
                return corpses ?? (corpses = new MountTuning(GK2DonkeyAPI.Corpses.Section, GK2DonkeyAPI.Corpses.Settings));
            }
        }

        // Whether the corpse delivery is hooked right now (CorpsesAtCrossroads on, the API active, the patches in).
        public static bool CorpsesActive => GK2DonkeyAPI.Corpses.Applied;

        // A body the game's donkey left at the crossroads (CorpsesAtCrossroads).
        public static event Action<CorpseInfo> CorpseLeft;

        internal static void RaiseCorpseLeft(CorpseInfo corpse) => Raise(CorpseLeft, corpse, "CorpseLeft");

        // Debug aid: starts the game donkey's own delivery now (the same event the game's schedule sends every second
        // day), with its body roll forced to pass. The game's other rules still apply (the morgue's capacity, the
        // quests). Not sent while the donkey is already out. Returns what happened, for the log.
        public static string StartCorpseDelivery() => GK2DonkeyAPI.Corpses.StartDeliveryNow();

        // The colours of the donkey, the cart and the lantern, for both styles, overridable like tuning. Keys: the
        // globals Enabled (bool), SpriteMethod (SpriteColorMethod), GlowColor, LitGlassTint, CharacterLightColor (hex
        // RRGGBB, "" = the game's), then for every region of ColorRegions its KeyOf(setting) keys ("CoatHue",
        // "HarnessSwaps", ...; MountColorRegion.SettingNames says what each does). Changes show within a frame.
        public static MountTuning Colors
        {
            get
            {
                if (GK2DonkeyAPI.Colors.Enabled == null)
                {
                    throw new InvalidOperationException("GK2 Donkey API has not loaded its settings yet.");
                }
                return GK2DonkeyAPI.Colors.Tuning;
            }
        }

        // The skins a donkey can wear: the list, a lookup, the loader's report (docs/skins-design.md). A donkey wears one
        // through its tuning key "Skin" or MountCompanion.Skin.
        public static MountLooks Looks { get; } = new MountLooks();

        // Particle effects (docs/api.md "Effects"): the list, registering one, the game's particles as a source. Nothing
        // picks an effect for a mount in this version (2026-10-25: the player's rows, the Effect keys and the packs'
        // effect = went; how a skin comes with one is being designed), so a registered effect shows nowhere yet.
        public static MountEffects Effects { get; } = new MountEffects();

        // The mount sounds (docs/mount-sounds-design.md): the catalogue of events, what plays for one on an instance,
        // a mod's own sound for one, Play and Stop, the Played event.
        public static MountSounds Sounds { get; } = new MountSounds();

        // Mounts (docs/mounts-design.md): what is ridden as a creature type, with its seat, rules and abilities;
        // a skin is a look of one mount.
        public static MountCatalog Catalog { get; } = new MountCatalog();

        // Always true since 0.40.0. In 0.30.0 to 0.39.0 the mounts other than the donkey, the skins and the effects could
        // be switched off (Saddle Up 0.10.0 shipped them so, by the dev key Debug.MountsPreview): the lists held the
        // donkey alone and a registration was kept out. Since then mounts are mods of their own (packs), and which ones
        // a player has is which they installed; the property stays for mods written against 0.10.0.
        public static bool MountsEnabled => true;

        // Whether jumping and flying are switched on (0.36.0; docs/flying.md): on for everyone since 0.38.0 (a dev-only
        // experiment before, off for players), false only when the dev tab switched it off. A mod showing its own jump
        // or fly key rows shows them only while this is true.
        public static bool FlyingEnabled => Flight.Active;

        // The ridden mount off the ground (0.36.0; refused while FlyingEnabled is false): what the jump and fly keys do,
        // for a mod's own button or a script. Jump: a full jump (the key's hold-for-height is the key's); refused off a
        // mount that doesn't jump, in the air, with the cart. TakeOff: from the ground, or from a jump in the air (the
        // jump becomes the take-off); refused off a mount that doesn't fly, indoors (FlyIndoors), with the cart, already
        // flying. Land: while taking off or flying; it lands where the keeper can stand (else it hovers, "Nowhere to land
        // here."). The reason is in the result; nothing is said in the bubble.
        public static MountResult Jump() => Flight.ApiJump();
        public static MountResult TakeOff() => Flight.ApiTakeOff();
        public static MountResult Land() => Flight.ApiLand();

        // Where the ridden mount is: on the ground, jumping, taking off, flying, landing (always Ground while not riding).
        public static MountFlight FlightState => Flight.State;

        // FlightState changed (argument: the new one): a jump (Jumping, then Ground), a take-off (TakingOff, Flying), a
        // landing (Landing, Ground), a jump turned flight (Jumping, TakingOff). Handlers that throw are logged and skipped.
        public static event Action<MountFlight> FlightChanged;
        internal static void RaiseFlightChanged(MountFlight s) => Raise(FlightChanged, s, "FlightChanged");

        // Never lost (0.36.0, docs/never-lost.md). The API records the spots the keeper stood on (the walkable ground
        // under his feet, nothing over him, his body fitting; a ring of eight per scene, plus where the game put him at
        // a load and after each door) and, with the Behaviour key FallGuard on, puts him back on the newest one that
        // still holds when he has fallen out of the world. ReturnToSafeSpot does the same by hand (a mod's button or key;
        // Saddle Up's "Back to the last safe spot"): refused, with the reason, while not in a game, while a door, a
        // cutscene or a dialogue has the keeper, and in a scene with no spot yet. A flight or a jump is ended first; a
        // ride goes on (the mount follows). Every return is a warning in the log with its facts.
        public static MountResult ReturnToSafeSpot(string reason = null)
        {
            MountResult r = SafeSpots.Return(string.IsNullOrEmpty(reason) ? "asked by a mod" : reason);
            if (!r.Ok) Mount.Say(r.Reason, 3f); // "Back on solid ground." is said by the return itself
            return r;
        }

        // The newest safe spot of the keeper's scene, or null (none recorded yet).
        public static Vector3? LastSafeSpot => SafeSpots.Newest(out Vector3 p) ? p : (Vector3?)null;

        // The keeper was put back on a safe spot (argument: where), by the watchdog, a key, a mod or Saddle Up's button.
        public static event Action<Vector3> ReturnedToSafeSpot;
        internal static void RaiseReturnedToSafeSpot(Vector3 p) => Raise(ReturnedToSafeSpot, p, "ReturnedToSafeSpot");

        // A registration called before the API's own Awake (a mod without [BepInDependency] that BepInEx loaded first):
        // nothing of the API is up yet, and its log isn't either. Said by throwing, with what to add.
        internal static void RequireStarted(string ownerId, string what, string id)
        {
            if (Plugin.Log == null) throw new InvalidOperationException($"GK2 Donkey API has not started yet ({ownerId}'s {what} '{id}'): add [BepInDependency(\"{Plugin.Guid}\")] to your plugin.");
        }

        // The colour regions, in panel order: Coat, Dark, Muzzle, Eye, Harness (the donkey), CartBody, Shafts, Straps,
        // Hooks, Wheels, Hubs (the cart), Pole, Lamp, Glass (the lantern).
        public static IReadOnlyList<MountColorRegion> ColorRegions => GK2DonkeyAPI.Colors.ApiRegions ?? Array.Empty<MountColorRegion>();

        // Removes every tuning, behaviour and colour override this owner set, on everyone's values and on every
        // instance's own.
        public static void ClearTuning(string ownerId)
        {
            Tuning(MountStyle.Ride).ClearOwner(ownerId);
            Tuning(MountStyle.Cart).ClearOwner(ownerId);
            Behaviour.ClearOwner(ownerId);
            Shuffle.ClearOwner(ownerId);
            Corpses.ClearOwner(ownerId);
            Colors.ClearOwner(ownerId);
            foreach (MountHandle h in Instances)
            {
                h.Tuning.ClearOwner(ownerId);
            }
        }

        // ------------------------------------------------------------------ instances in the world

        // A donkey (withCart: with its cart) standing at position in the keeper's current scene, with its own settings
        // (MountHandle.Tuning). The keeper rides it with the interact key next to it, or MountHandle.Ride. Null outside
        // a game or while the core is off.
        public static MountHandle SpawnDonkey(string ownerId, Vector3 position, bool withCart = false, bool facingRight = true)
        {
            return Spawn(ownerId, position, hasDonkey: true, hasCart: withCart, facingRight: facingRight);
        }

        // A cart on its own (no donkey), shafts on the ground, at position in the keeper's current scene. A donkey
        // takes it with Hitch, or the keeper rides one up to it and presses the interact key (or General.HitchKey).
        public static MountHandle SpawnCart(string ownerId, Vector3 position, bool facingRight = true)
        {
            return Spawn(ownerId, position, hasDonkey: false, hasCart: true, facingRight: facingRight);
        }

        // Any instance, with every property (MountSpawn): a donkey, a donkey with its cart, a free cart (Donkey = false),
        // and the foal properties (Scale, Rideable, Parent). Null when refused; the log says why.
        public static MountHandle Spawn(string ownerId, MountSpawn spec)
        {
            if (string.IsNullOrEmpty(ownerId))
            {
                throw new ArgumentException("An owner id (your plugin GUID) is required.", nameof(ownerId));
            }
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }
            if (!IsAvailable)
            {
                return null;
            }
            if (spec.Parent != null && !spec.Parent.Exists)
            {
                Plugin.Log.LogInfo($"API: {ownerId} could not spawn: the parent is gone.");
                return null;
            }
            // a mount by name: its Default skin (the skin whose id is the mount's, else the first declaring it)
            string skinId = spec.Skin;
            if (string.IsNullOrEmpty(skinId) && !string.IsNullOrEmpty(spec.Mount))
            {
                GK2DonkeyAPI.Catalog.Entry mountByName = GK2DonkeyAPI.Catalog.Find(spec.Mount);
                if (mountByName == null)
                {
                    Plugin.Log.LogInfo($"API: {ownerId} could not spawn: no mount '{spec.Mount}'.");
                    return null;
                }
                skinId = mountByName == GK2DonkeyAPI.Catalog.Donkey ? null : GK2DonkeyAPI.Looks.DefaultSkinOf(mountByName);
                if (skinId == MountLooks.DonkeyId)
                {
                    // a mount with no skin has no drawing: not offered to players (Looks.MountChoices), not spawned by name
                    Plugin.Log.LogInfo($"API: {ownerId} could not spawn: the mount '{spec.Mount}' has no skin.");
                    return null;
                }
            }
            // the cart rule (docs/mounts-design.md §3): a mount that can't pull the cart is not spawned with one
            if (spec.WithCart && spec.Donkey && !string.IsNullOrEmpty(skinId))
            {
                GK2DonkeyAPI.Looks.SkinDef sd = GK2DonkeyAPI.Looks.Find(skinId);
                if (sd != null && !sd.Mount.Cart)
                {
                    Plugin.Log.LogInfo($"API: {ownerId} could not spawn with a cart: a {sd.Mount.NounText} can't pull the cart.");
                    return null;
                }
            }
            Mount.Instance i = Mount.Spawn(new Mount.SpawnArgs
            {
                Owner = ownerId,
                Position = spec.Position,
                HasDonkey = spec.Donkey,
                HasCart = spec.WithCart,
                FacingRight = spec.FacingRight,
                Scale = spec.Scale,
                Rideable = spec.Rideable,
                Parent = spec.Parent?.I,
                RemoveWithParent = spec.RemoveWithParent,
                SceneId = spec.SceneId,
                Persist = spec.Persist,
                Key = spec.Key,
                Id = spec.Id,
            }, out string why);
            if (i == null)
            {
                Plugin.Log.LogInfo($"API: {ownerId} could not spawn: {why}");
                return null;
            }
            if (!string.IsNullOrEmpty(skinId) && spec.Donkey)
            {
                i.Handle.Tuning.Set(ownerId, "Skin", skinId); // the skin, and with it the mount it is a look of
            }
            return i.Handle;
        }

        // A foal of a donkey in the world: small (scale, 0.6 by default), not rideable, next to its parent, and with its
        // own idle shuffle switched on and made for a foal (smaller, quicker steps, a wider area around the parent; its
        // own settings, owned by ownerId, changeable through MountHandle.Tuning). It trots after the parent when the
        // parent moves, and goes when the parent is removed. Null when refused (see Mounts.Spawn).
        // id, key: as MountSpawn. persist: kept with the save; null: as its parent is. scale: the baby's size as a fraction
        // of the grown one; 0 (the default) is the parent's mount's own (MountDef.FoalScale: the donkey's 0.6, Gerry's 0.33).
        public static MountHandle SpawnFoal(string ownerId, MountHandle parent, float scale = 0f, int id = 0, string key = null, bool? persist = null)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }
            if (!parent.Exists)
            {
                return null;
            }
            if (scale <= 0f) scale = Catalog.Find(parent.Mount)?.FoalScale ?? 0.6f;
            Mount.Instance p = parent.I;
            Vector3 at = p.BodyPoint() + new Vector3(UnityEngine.Random.Range(-1.2f, 1.2f), 0f, UnityEngine.Random.Range(-0.5f, 0.5f));
            MountHandle foal = Spawn(ownerId, new MountSpawn
            {
                Position = at,
                FacingRight = UnityEngine.Random.value < 0.5f,
                Scale = scale,
                Rideable = false,
                Parent = parent,
                Persist = persist ?? p.Persist,
                Id = id,
                Key = key,
            });
            if (foal == null)
            {
                return null;
            }
            foal.I.PlaceNearParent(); // a free slot next to the parent, checked for ground and room
            InstanceTuning t = foal.Tuning;
            t.Set(ownerId, "IdleShuffle", true);
            t.Set(ownerId, "ShuffleAreaWidth", 0.9f);
            t.Set(ownerId, "ShuffleAreaDepth", 0.45f);
            t.Set(ownerId, "ShuffleStepMin", 0.2f);
            t.Set(ownerId, "ShuffleStepMax", 0.6f);
            t.Set(ownerId, "ShuffleSpeed", 0.45f);
            t.Set(ownerId, "ShufflePauseMin", 2.5f);
            t.Set(ownerId, "ShufflePauseMax", 8f);
            t.Set(ownerId, "ShuffleTurnChance", 0.35f);
            return foal;
        }

        private static MountHandle Spawn(string ownerId, Vector3 position, bool hasDonkey, bool hasCart, bool facingRight)
        {
            if (string.IsNullOrEmpty(ownerId))
            {
                throw new ArgumentException("An owner id (your plugin GUID) is required.", nameof(ownerId));
            }
            if (!IsAvailable)
            {
                return null;
            }
            Mount.Instance i = Mount.Spawn(new Mount.SpawnArgs { Owner = ownerId, Position = position, HasDonkey = hasDonkey, HasCart = hasCart, FacingRight = facingRight }, out string why);
            if (i == null)
            {
                Plugin.Log.LogInfo($"API: {ownerId} could not spawn: {why}");
            }
            return i?.Handle;
        }

        // ------------------------------------------------------------------ identity and the game being ready

        // The game is loaded and the save's kept donkeys are back (InstancesRestored has come): spawning works from now
        // on. False in the menu, while a game loads, and for a frame at a door that loads another scene.
        public static bool IsGameReady => Mount.IsGameReady;

        // Your mod's companion: the keeper's own donkey with its cart and foal, its keys, looks and ride settings
        // (MountCompanion). One per owner; the same object on every call.
        public static MountCompanion Companion(string ownerId)
        {
            MountCompanion c = MountCompanion.For(ownerId);
            KeyActions.Track(ownerId); // unloaded, its tuning is cleared (KeyActions)
            return c;
        }

        // Your mod's player settings (MountSettingsPage): in GK2 Mod Framework's Mods menu when it's installed, else
        // in the game's own Settings window. title: shown to players; status: what the Status row shows (page.Status)
        // and "Active" when all is well (the Mods menu then confirms the game build); without it, StatusText. Add rows,
        // then Publish.
        public static MountSettingsPage SettingsPage(string ownerId, BepInEx.Configuration.ConfigFile config, string title, string author, string description, Func<string> status = null)
        {
            if (string.IsNullOrEmpty(ownerId))
            {
                throw new ArgumentException("An owner id (your plugin GUID) is required.", nameof(ownerId));
            }
            return new MountSettingsPage(ownerId, config ?? throw new ArgumentNullException(nameof(config)), title ?? ownerId, author ?? "", description ?? "", status);
        }

        // Once per loaded game (a save, or a new game), after the save's kept donkeys are back, before anything else can
        // spawn: the place for a mod's own per-game spawning. Check Find(yourId, id) first; your kept donkeys are
        // already there. Spawning before it is refused (null, "Not until the game is ready").
        public static event Action GameReady;

        internal static void RaiseGameReady()
        {
            if (GameReady == null)
            {
                return;
            }
            foreach (Delegate d in GameReady.GetInvocationList())
            {
                try
                {
                    ((Action)d)();
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"API: a GameReady handler failed: {e}");
                }
            }
        }

        // An owner's instance by its number (MountHandle.Id) or its key (MountHandle.Key), or null.
        public static MountHandle Find(string ownerId, int id) => Mount.FindInstance(ownerId, id)?.Handle;

        public static MountHandle Find(string ownerId, string key) => Mount.FindInstance(ownerId, key)?.Handle;

        // Every instance of one owner, in spawn order.
        public static IReadOnlyList<MountHandle> InstancesOf(string ownerId)
        {
            if (ownerId == null)
            {
                return new List<MountHandle>();
            }
            if (ofOwnerVersion != Mount.structureVersion)
            {
                ofOwner.Clear();
                ofOwnerVersion = Mount.structureVersion;
            }
            if (!ofOwner.TryGetValue(ownerId, out List<MountHandle> l))
            {
                l = new List<MountHandle>();
                foreach (Mount.Instance i in Mount.All)
                {
                    if (!i.Ephemeral && i.OwnerId == ownerId && i.Handle != null)
                    {
                        l.Add(i.Handle);
                    }
                }
                ofOwner[ownerId] = l;
            }
            return l;
        }

        // Cached per change of the set of instances (Mount.structureVersion): a mod reading Instances or InstancesOf
        // every frame no longer builds a list every frame. Each change makes new lists, so a list a caller holds stays
        // as it was (a snapshot, as before), also while it removes instances in a loop over it.
        private static List<MountHandle> allHandles;
        private static int allVersion = -1, ofOwnerVersion = -1;
        private static readonly Dictionary<string, List<MountHandle>> ofOwner = new Dictionary<string, List<MountHandle>>(StringComparer.Ordinal);

        // Owner-only changes (MountHandle.SetPersist, SetKey): its owner, or the core and its panel.
        // The owner's own code, by its assembly: the API's, or the plugin whose GUID owns the instance. An owner that
        // isn't a loaded plugin's GUID (a mod's own sub-owner) can't be told apart: allowed.
        internal static string WhyNotCaller(MountHandle h, System.Reflection.Assembly caller)
        {
            if (!h.Exists || caller == null || caller == typeof(Mounts).Assembly || Mount.IsCoreOwner(h.I.OwnerId)) return null;
            try
            {
                if (!BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(h.I.OwnerId, out BepInEx.PluginInfo p) || p?.Instance == null) return null;
                return p.Instance.GetType().Assembly == caller ? null : $"it belongs to {h.I.OwnerId}; only its owner removes it (Remove(ownerId)).";
            }
            catch
            {
                return null;
            }
        }

        private static readonly HashSet<string> warnedOnce = new HashSet<string>(StringComparer.Ordinal);

        // Which mod's code uses an owner id: two mods built from one example with its GUID kept replace and withdraw
        // each other's mounts and skins without a word (docs/multi-mod-loading.md 4.6); said once.
        private static readonly Dictionary<string, System.Reflection.Assembly> ownerAssemblies = new Dictionary<string, System.Reflection.Assembly>(StringComparer.Ordinal);

        internal static void NoteOwner(string ownerId, System.Reflection.Assembly asm)
        {
            if (string.IsNullOrEmpty(ownerId) || asm == null || asm == typeof(Mounts).Assembly) return;
            if (!ownerAssemblies.TryGetValue(ownerId, out System.Reflection.Assembly first))
            {
                ownerAssemblies[ownerId] = asm;
                return;
            }
            if (first != asm) WarnOnce("owner " + ownerId, $"Mounts: the owner id '{ownerId}' is used by two mods ({first.GetName().Name} and {asm.GetName().Name}): each replaces and withdraws the other's mounts and skins. Give each mod its own plugin GUID.");
        }

        internal static void WarnOnce(string key, string text)
        {
            if (warnedOnce.Add(key)) Plugin.Log.LogWarning(text);
        }

        internal static string WhyNotOwner(MountHandle h, string ownerId)
        {
            if (string.IsNullOrEmpty(ownerId))
            {
                throw new ArgumentException("An owner id (your plugin GUID) is required.", nameof(ownerId));
            }
            if (!h.Exists)
            {
                return "It is gone.";
            }
            if (ownerId == h.I.OwnerId || Mount.IsCoreOwner(ownerId))
            {
                return null;
            }
            return $"Only its owner ({h.I.OwnerId}) can change that.";
        }

        // Every instance in the world (not the summon of TryMount), in spawn order.
        public static IReadOnlyList<MountHandle> Instances
        {
            get
            {
                if (allHandles == null || allVersion != Mount.structureVersion)
                {
                    var l = new List<MountHandle>();
                    foreach (Mount.Instance i in Mount.All)
                    {
                        if (!i.Ephemeral && i.Handle != null)
                        {
                            l.Add(i.Handle);
                        }
                    }
                    allHandles = l;
                    allVersion = Mount.structureVersion;
                }
                return allHandles;
            }
        }

        // The instance being ridden; null while on foot or riding the summon of TryMount.
        public static MountHandle Riding => Mount.Ridden != null && !Mount.Ridden.Ephemeral ? Mount.Ridden.Handle : null;

        // The donkey takes the free cart: the cart's cargo and cart settings come with it, the free cart is gone. A
        // standing donkey moves to the cart; a ridden one is put there with the keeper.
        public static MountResult Hitch(MountHandle donkey, MountHandle cart)
        {
            if (donkey == null || cart == null || !donkey.Exists || !cart.Exists)
            {
                return MountResult.Refused("Gone.");
            }
            if (!Mount.Instance.IsInGame(out PlayerController pc, out _))
            {
                return MountResult.Refused("Not in a game.");
            }
            string why;
            using (Mount.Bind(donkey.I))
            {
                why = donkey.I.Hitch(cart.I, pc);
            }
            return why == null ? MountResult.Success : MountResult.Refused(why);
        }

        // The donkey leaves its cart standing where it is (a new free cart with the cargo and the cart settings) and
        // goes on without it. Returns the new cart, or null (not a donkey with a cart, or not loaded right now).
        public static MountHandle Unhitch(MountHandle donkey)
        {
            if (donkey == null || !donkey.Exists)
            {
                return null;
            }
            return UnhitchInstance(donkey.I);
        }

        // The same for whatever is ridden, the summon of TryMount included.
        public static MountHandle UnhitchRidden()
        {
            return UnhitchRidden(out _);
        }

        // why: the reason when it's refused (for the companion's thought bubble).
        internal static MountHandle UnhitchRidden(out string why)
        {
            why = "Not riding.";
            return Mount.Ridden != null ? UnhitchInstance(Mount.Ridden, out why) : null;
        }

        private static MountHandle UnhitchInstance(Mount.Instance i)
        {
            return UnhitchInstance(i, out _);
        }

        private static MountHandle UnhitchInstance(Mount.Instance i, out string why)
        {
            why = "Not in a game.";
            if (!Mount.Instance.IsInGame(out PlayerController pc, out _))
            {
                return null;
            }
            Mount.Instance cart;
            using (Mount.Bind(i))
            {
                cart = i.Unhitch(pc, out why);
            }
            if (cart == null)
            {
                Plugin.Log.LogInfo("API: unhitch refused: " + why);
            }
            return cart?.Handle;
        }

        // An instance was spawned (also: a cart left standing by unhitching) / is gone (removed, hitched away, the game
        // left). The ride's own Mounted / Dismounted events still come for instances too.
        public static event Action<MountHandle> InstanceSpawned;

        public static event Action<MountHandle> InstanceRemoved;

        // (donkey, the free cart it took, which no longer exists) / (donkey, the new free cart).
        public static event Action<MountHandle, MountHandle> Hitched;

        public static event Action<MountHandle, MountHandle> Unhitched;

        // A save was loaded and the instances kept with it are back (MountSpawn.Persist), each also announced by
        // InstanceSpawned. Their ids are new: find yours by Owner and Key.
        public static event Action<IReadOnlyList<MountHandle>> InstancesRestored;

        internal static void RaiseInstancesRestored(IReadOnlyList<MountHandle> handles)
        {
            if (InstancesRestored == null)
            {
                return;
            }
            foreach (Delegate d in InstancesRestored.GetInvocationList())
            {
                try
                {
                    ((Action<IReadOnlyList<MountHandle>>)d)(handles);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"API: an InstancesRestored handler failed: {e}");
                }
            }
        }

        internal static void RaiseInstanceSpawned(MountHandle h) => Raise(InstanceSpawned, h, "InstanceSpawned");

        internal static void RaiseInstanceRemoved(MountHandle h) => Raise(InstanceRemoved, h, "InstanceRemoved");

        internal static void RaiseHitched(MountHandle donkey, MountHandle cart) => Raise2(Hitched, donkey, cart, "Hitched");

        internal static void RaiseUnhitched(MountHandle donkey, MountHandle cart) => Raise2(Unhitched, donkey, cart, "Unhitched");

        private static void Raise2(Action<MountHandle, MountHandle> handlers, MountHandle a, MountHandle b, string name)
        {
            if (handlers == null)
            {
                return;
            }
            foreach (Delegate d in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<MountHandle, MountHandle>)d)(a, b);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"API: a {name} handler ({d.Method.DeclaringType?.Assembly.GetName().Name}) failed: {e.Message}");
                }
            }
        }

        private static MountTuning rideTuning, cartTuning, behaviour, shuffle, corpses;

        // Removes every rule this owner added.
        public static void RemoveRules(string ownerId)
        {
            MountRules.RemoveAll(ownerId);
        }

        // After the donkey is summoned (argument: the style). Handlers that throw are logged and skipped.
        public static event Action<MountStyle> Mounted;

        // After the ride ended, cargo handed back (argument: the reason, e.g. "dismounted", "indoors", a rule's text).
        public static event Action<string> Dismounted;

        // An item went onto the cart / left it (argument: the item, with its id, e.g. "wood", "body_corpse", its GUID
        // and, for a zombie, its name: two zombies are both "body_zombie"). Not raised for the core's own bookkeeping:
        // the hand-back around a save, or the cart summoned again after a door.
        public static event Action<CargoItem> CargoLoaded;

        public static event Action<CargoItem> CargoUnloaded;

        // A storage chest went onto a cart / was set down from it (docs/portable-chest.md; argument: the chest with
        // the cart it rides on, its id and GUID, and how many item stacks it holds). Raised for the player's own
        // loading and for every set-down, including the mod's fallbacks (the cart removed, a scene left). Not raised
        // for the core's own bookkeeping around a save or a load, nor when the chest changes carts at hitching. (0.24.0)
        public static event Action<ChestInfo> ChestLoaded;

        public static event Action<ChestInfo> ChestUnloaded;

        // ------------------------------------------------------------------ core side

        private static string owner;

        // A world instance's ride belongs to the mod that spawned it.
        internal static void SetRideOwner(string ownerId)
        {
            owner = ownerId;
        }

        internal static void RaiseMounted(MountStyle style)
        {
            Raise(Mounted, style, "Mounted");
        }

        internal static void RaiseDismounted(string reason)
        {
            owner = null;
            Raise(Dismounted, reason, "Dismounted");
        }

        internal static void RaiseCargoLoaded(CargoItem item)
        {
            Raise(CargoLoaded, item, "CargoLoaded");
        }

        internal static void RaiseCargoUnloaded(CargoItem item)
        {
            Raise(CargoUnloaded, item, "CargoUnloaded");
        }

        internal static void RaiseChestLoaded(ChestInfo chest)
        {
            Raise(ChestLoaded, chest, "ChestLoaded");
        }

        internal static void RaiseChestUnloaded(ChestInfo chest)
        {
            Raise(ChestUnloaded, chest, "ChestUnloaded");
        }

        private static void Raise<T>(Action<T> handlers, T arg, string name)
        {
            if (handlers == null)
            {
                return;
            }
            foreach (Delegate d in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<T>)d)(arg);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"API: a {name} handler ({d.Method.DeclaringType?.Assembly.GetName().Name}) failed: {e.Message}");
                }
            }
        }
    }

    // Rules added by other mods (internal; reached through Mounts.Add*Rule).
    internal static class MountRules
    {
        private sealed class Rule
        {
            public string Owner;
            public Func<MountStyle, string> Mount;
            public Func<string> Stay;
            public Func<bool> Feature;
            public MountFeature Which;
            public bool Warned;
        }

        private sealed class Handle : IDisposable
        {
            private Rule rule;

            public Handle(Rule r)
            {
                rule = r;
            }

            public void Dispose()
            {
                if (rule != null)
                {
                    rules.Remove(rule);
                    bool feature = rule.Feature != null;
                    rule = null;
                    if (feature)
                    {
                        Poll(force: true);
                    }
                }
            }
        }

        private static readonly List<Rule> rules = new List<Rule>();
        private static float nextStayCheck;
        private static float nextFeatureCheck;
        private static readonly int FeatureCount = Enum.GetValues(typeof(MountFeature)).Length;
        private static readonly bool[] allowed = NewAllowed();

        private static bool[] NewAllowed()
        {
            bool[] a = new bool[Enum.GetValues(typeof(MountFeature)).Length];
            for (int i = 0; i < a.Length; i++)
            {
                a[i] = true;
            }
            return a;
        }

        // The cached answer; refreshed by Poll (at mount, twice a second while riding, on RefreshRules).
        internal static bool Allowed(MountFeature f)
        {
            int i = (int)f;
            return i < 0 || i >= allowed.Length || allowed[i];
        }

        internal static IDisposable AddFeature(string ownerId, MountFeature f, Func<bool> fn)
        {
            IDisposable h = Add(new Rule { Owner = Check(ownerId, fn), Feature = fn, Which = f });
            Poll(force: true);
            return h;
        }

        // Re-asks the feature rules (when due, or now with force) and reacts to the features that changed.
        internal static void Poll(bool force)
        {
            float now = UnityEngine.Time.unscaledTime;
            if (!force && now < nextFeatureCheck)
            {
                return;
            }
            nextFeatureCheck = now + 0.5f;
            for (int i = 0; i < FeatureCount; i++)
            {
                MountFeature f = (MountFeature)i;
                bool ok = true;
                string by = null;
                foreach (Rule r in rules.ToArray())
                {
                    if (r.Feature == null || r.Which != f)
                    {
                        continue;
                    }
                    bool yes;
                    try
                    {
                        yes = r.Feature();
                    }
                    catch (Exception e)
                    {
                        if (!r.Warned)
                        {
                            r.Warned = true;
                            Plugin.Log.LogWarning($"API: a feature rule of {r.Owner} failed and is ignored (it allows): {e.Message}");
                        }
                        yes = true;
                    }
                    if (!yes)
                    {
                        ok = false;
                        by = r.Owner;
                        break;
                    }
                }
                if (ok != allowed[i])
                {
                    allowed[i] = ok;
                    Plugin.Log.LogInfo($"API: feature {f} {(ok ? "allowed again" : "switched off by " + by)}.");
                    Mount.OnFeatureChanged(f, ok);
                }
            }
        }

        internal static IDisposable AddMount(string ownerId, Func<MountStyle, string> f)
        {
            return Add(new Rule { Owner = Check(ownerId, f), Mount = f });
        }

        internal static IDisposable AddStay(string ownerId, Func<string> f)
        {
            return Add(new Rule { Owner = Check(ownerId, f), Stay = f });
        }

        internal static void RemoveAll(string ownerId)
        {
            rules.RemoveAll(r => r.Owner == ownerId);
            Poll(force: true);
        }

        private static string Check(string ownerId, object f)
        {
            if (string.IsNullOrEmpty(ownerId))
            {
                throw new ArgumentException("An owner id (your plugin GUID) is required.", nameof(ownerId));
            }
            if (f == null)
            {
                throw new ArgumentNullException("rule");
            }
            return ownerId;
        }

        private static IDisposable Add(Rule r)
        {
            rules.Add(r);
            return new Handle(r);
        }

        // Null = every rule allows. A rule that throws allows (it is logged once): the rules only restrict, the
        // core's safety checks never depend on them.
        internal static string WhyCannotMount(MountStyle style)
        {
            foreach (Rule r in rules.ToArray())
            {
                if (r.Mount == null)
                {
                    continue;
                }
                string why = Ask(r, () => r.Mount(style));
                if (why != null)
                {
                    return why;
                }
            }
            return null;
        }

        // Polled from the mount's frame update; asks the stay rules at most twice a second.
        internal static string WhyMustDismountIfDue()
        {
            float now = UnityEngine.Time.unscaledTime;
            if (now < nextStayCheck)
            {
                return null;
            }
            nextStayCheck = now + 0.5f;
            foreach (Rule r in rules.ToArray())
            {
                if (r.Stay == null)
                {
                    continue;
                }
                string why = Ask(r, r.Stay);
                if (why != null)
                {
                    return why;
                }
            }
            return null;
        }

        private static string Ask(Rule r, Func<string> f)
        {
            try
            {
                string why = f();
                return string.IsNullOrEmpty(why) ? null : why;
            }
            catch (Exception e)
            {
                if (!r.Warned)
                {
                    r.Warned = true;
                    Plugin.Log.LogWarning($"API: a rule of {r.Owner} failed and is ignored (it allows): {e.Message}");
                }
                return null;
            }
        }
    }
}
