using System;
using BepInEx;
using BepInEx.Configuration;
using GK2DonkeyAPI.Api;
using UnityEngine;

namespace SaddleUp
{
    // The player mod: the keeper's own donkey, cart and foal, built only on the public GK2 Donkey API (docs/api.md).
    // The API does the work (Mounts.Companion: calling, riding, hitching, looks, keeping it with the save; and
    // Mounts.SettingsPage: the Mods menu with GK2 Mod Framework, else the game's own Settings window, its keys polled and
    // the mod's tuning cleared if it's unloaded). This mod only lists its settings and what its keys do, so it's also
    // the example of how little a player mod needs.
    [BepInPlugin(Guid, "GK2 Saddle Up", Version)]
    [BepInDependency("dreas.gk2.donkeyapi", "0.30.0")] // at least the API it was written against (NeedsApi below)
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "dreas.gk2.saddleup";
        // This mod's own version; the GK2 Donkey API counts its own (Mounts.ApiVersion), and NeedsApi says which one this
        // mod was written against.
        public const string Version = "0.10.0";

        // The API this mod is written against: 0.30 has the skins (MountCompanion.Skin; 0.28 CartSlots, 0.27 CorpseIntoMorgue, 0.23 CargoChest, 0.22
        // CargoZombies). Checked before anything of the API is used, so an older API gets a clear message instead of a
        // missing-method error. Keep it the same as the BepInDependency minimum above.
        private static readonly Version NeedsApi = new Version(0, 30, 0);

        private void Awake()
        {
            if (Mounts.ApiVersion < NeedsApi)
            {
                Logger.LogError($"GK2 Donkey API {Mounts.ApiVersion} is too old (this mod needs {NeedsApi} or newer). Update GK2 Donkey API. The mount is off.");
                return;
            }
            MountCompanion me = Mounts.Companion(Guid);
            Mounts.Behaviour.Set(Guid, "StayMountedThroughDoors", false); // not offered until it's tested
            Mounts.Behaviour.Set(Guid, "HitchKey", KeyCode.None); // H and G switch riding and driving; E hitches a cart in reach
            Mounts.Behaviour.Set(Guid, "DismountIndoors", true); // always outside only (the API can allow it; Saddle Up doesn't offer it)

            MountSettingsPage p = Mounts.SettingsPage(Guid, Config, "GK2 Saddle Up", "dreas",
                Mounts.MountsEnabled
                    ? "Your own mount and cart: ride it (H), drive the cart (G), haul with it. A donkey, or any mount a skin pack or another mod adds (dragons, a giant rat...). Built on GK2 Donkey API."
                    : "Your own donkey and cart: ride it (H), drive the cart (G), haul with it. Other mounts, skins and effects come in the next update. Built on GK2 Donkey API.");
            p.Status("Keys");
            ConfigEntry<KeyboardShortcut> rideKey = p.Key("Keys", "RideKey", new KeyboardShortcut(KeyCode.H), "Ride your mount", "Your mount comes and you ride it (a parked cart stays where it is); again: dismount. In the cart: leave the cart standing and ride on.",
                pressed: () => me.Use(MountStyle.Ride));
            ConfigEntry<KeyboardShortcut> cartKey = p.Key("Keys", "CartKey", new KeyboardShortcut(KeyCode.G), "Drive the cart", "Your mount comes with its cart and you drive; again: dismount. Riding: the cart comes and is hitched.",
                pressed: () => me.Use(MountStyle.Cart));
            p.Key("Keys", "BellKey", new KeyboardShortcut(KeyCode.B), "Ring the bell", "While mounted. None = no key.", pressed: me.RingBell);
            p.Key("Keys", "SendAwayKey", KeyboardShortcut.Empty, "Send your mount away", "Your mount, its cart and its babies go (the cart's load is set down first); the next key brings a new one. None = no key.",
                pressed: me.SendAway);

            // Right after the keys, your mount: its picture and name, what it is (Mount), how it is drawn (Skin), its
            // babies; then Effect, the particle effect on it. The name and the baby rows were in the Donkey section
            // before 0.11 (MovedKey carries a player's values over); moving sections renumbers them in the cfg, and the
            // page carries those over too.
            p.MovedKey("Mount", "Name", "Donkey");
            p.MovedKey("Mount", "Foals", "Donkey");
            p.MovedKey("Mount", "FoalsIndoors", "Donkey");
            p.Preview("Mount", me, "Your mount", cart: false);
            p.Text("Mount", "Name", me.Name ?? "", "Name", "Shown when you walk up to it: \"[E] Ride Bella\". Empty: no name.", v => me.Name = v);
            p.MountRows("Mount", me);
            if (!Mounts.MountsEnabled)
            {
                // This version ships the mounts, skins and effects switched off (Mounts.MountsEnabled; the next update
                // switches them on): the rows above hold the donkey alone, and these say why.
                p.Info("Mount", "Other mounts and skins", () => "Coming in the next update",
                    "A dragon, a giant rat, a giant snail, a giant worm, the Hellfire skin and skin packs are built and switched off in this version; the next update switches them on. Until then your mount is the donkey, as before.");
            }
            p.FoalRows("Mount", me);
            if (!Mounts.MountsEnabled)
            {
                p.Info("Effect", "Particle effects", () => "Coming in the next update",
                    "Hellfire (the game's lamp fire in a ring round your mount, with sparks and a trail) and effects from mods are built and switched off in this version; the next update switches them on.");
            }
            p.EffectRows("Effect", me);

            // The companion's own values are the defaults: written once, in the API (MountCompanion).
            void Speed(MountStyle style, string key, string label, string how, MountSpeed now, Action<MountSpeed> set) =>
                p.Choice("Behaviour", key, now, label, "How fast you go " + how + ".", set, MountCompanion.SpeedLabelsFor(style));
            Speed(MountStyle.Ride, "RideSpeed", "Riding speed", "riding your mount", me.RideSpeed, v => me.RideSpeed = v);
            Speed(MountStyle.Cart, "CartSpeed", "Cart speed", "driving the cart", me.CartSpeed, v => me.CartSpeed = v);
            p.Toggle("Behaviour", "ShowMessages", true, "Say why mounting is refused", "The keeper's thought bubble.", v => Mounts.Behaviour.Set(Guid, "ShowMessages", v));
            p.Toggle("Behaviour", "CargoZombies", true, "Zombies ride on the cart", "Pick up a zombie while driving and it lies on the cart, as many as the cart holds; hand it to its work straight from the cart. Off: the cart refuses zombies.", v => Mounts.Behaviour.Set(Guid, "CargoZombies", v));
            p.Toggle("Behaviour", "CorpseIntoMorgue", true, "Carry the corpse into the morgue", "Driving the cart through the morgue door with a corpse on it: you get off at the door as always and the cart stays outside with its load, but you walk in carrying the corpse. Off: it stays on the cart.", v => Mounts.Behaviour.Set(Guid, "CorpseIntoMorgue", v));
            p.Toggle("Behaviour", "CargoChest", true, "A storage chest rides on the cart", "Drive up to a plain storage chest (rough, normal or good) and press the action key: it rides on the cart with everything inside, alone. The interact key with nothing in front sets it down. Off: chests stay where they are.", v => Mounts.Behaviour.Set(Guid, "CargoChest", v));
            p.Steps("Behaviour", "CartSlots", 6, 2, 6, 2, "Cart capacity", "How many items the cart carries: 6 (three layers of two), 4 (two layers) or 2 like the game's own delivery cart. Piles, tables and stations take from all of them; a chest aboard still takes the whole cart. Lowering it never drops anything: a fuller cart stays full until unloaded.", v => Mounts.Behaviour.Set(Guid, "CartSlots", v), v => v + " items");
            p.Toggle("Behaviour", "IdleShuffle", true, "Shuffles while standing", "Left without its cart, it takes a few slow steps now and then.", v => Mounts.Behaviour.Set(Guid, "IdleShuffle", v));
            p.Toggle("Behaviour", "KeepWithSaves", me.KeepWithSaves, "Keep your mount with the save", "After loading, your mount and cart are where you left them, the load on the cart. They're kept in a small file next to each save (<save>.gk2donkeyapi.dat), which Steam Cloud syncs along with your saves.", v => me.KeepWithSaves = v);
            p.Toggle("Behaviour", "CartStaysParked", me.CartStaysParked, "Calling your mount leaves the cart parked", "Ride key while your mount stands somewhere with the cart: it comes alone and the cart stays where you left it, with its load. Off: both come and the cart is left standing next to you.", v => me.CartStaysParked = v);
            p.Toggle("Behaviour", "CorpsesAtCrossroads", false, "Donkey leaves corpses at the crossroads", "The game's delivery donkey leaves the bodies it brings at the crossroads by the nun, where it appears, instead of putting them on the morgue pallets. You carry them to the morgue yourself, or cart them. Off: the game's own delivery.", v => Mounts.Corpses.Set(Guid, "CorpsesAtCrossroads", v));

            // The mount's colours: the same rows for every mount (docs/mount-colours.md), kept per mount (PerMount: the
            // donkey's rows keep their keys, the dragon's are Coat_dragon and so on). Coat is the body (the donkey's
            // coat, a dragon's scales, the worm's skin); Harness its tack, on the mounts whose look has any.
            p.LookButton("Donkey", "Choose colours", "Closes this and opens the colours over the game, so you see your mount and cart change. Done keeps them, Cancel puts them back.");
            p.Preview("Cart", me, "Your cart");
            p.PerMount((mountId, sfx, shown) =>
            {
                MountLook look = me.LookOf(mountId);
                ConfigEntry<CoatColor> coat = p.Choice("Donkey", "Coat" + sfx, look.Coat, "Coat colour", "The mount's body: the donkey's coat, a dragon's scales, the worm's skin. Kept per mount.", v => look.Coat = v, new[] { "Game default", "Brown", "Dark", "Cream", "Black", "Custom" }, look: true);
                p.Color("Donkey", "CustomCoat" + sfx, "Custom coat colour", "Used when the coat colour is Custom.", PaletteColor.Chestnut, v => look.CustomCoat = v, () => coat.Value == CoatColor.Custom);
                ConfigEntry<HarnessColor> harness = p.Choice("Donkey", "Harness" + sfx, look.Harness, "Harness colour", "The mount's tack (the donkey's harness), on the mounts whose look has any; the others ignore it. Kept per mount.", v => look.Harness = v, new[] { "Game default", "Black", "Red", "Blue", "Green", "Custom" }, look: true);
                p.Color("Donkey", "CustomHarness" + sfx, "Custom harness colour", "Used when the harness colour is Custom.", PaletteColor.Wine, v => look.CustomHarness = v, () => harness.Value == HarnessColor.Custom);

                ConfigEntry<CartPaint> paint = p.Choice("Cart", "Paint" + sfx, look.Paint, "Paint", "The cart's paint with this mount.", v => look.Paint = v, new[] { "Game default", "Dark wood", "Red", "Green", "Blue", "Custom" }, look: true);
                p.Color("Cart", "CustomPaint" + sfx, "Custom paint", "Used when the paint is Custom.", PaletteColor.Brick, v => look.CustomPaint = v, () => paint.Value == CartPaint.Custom);
                ConfigEntry<WheelColor> wheels = p.Choice("Cart", "Wheels" + sfx, look.Wheels, "Wheel colour", "The wood of the wheels.", v => look.Wheels = v, new[] { "Game default", "Dark wood", "Red", "Green", "Blue", "Black", "Custom" }, look: true);
                p.Color("Cart", "CustomWheels" + sfx, "Custom wheel colour", "Used when the wheel colour is Custom.", PaletteColor.Mustard, v => look.CustomWheels = v, () => wheels.Value == WheelColor.Custom);
                ConfigEntry<HubColor> hubs = p.Choice("Cart", "Hubs" + sfx, look.Hubs, "Wheel hub colour", "The iron in the middle of the wheels.", v => look.Hubs = v, new[] { "Game default", "Black", "Brass", "Silver", "Custom" }, look: true);
                p.Color("Cart", "CustomHubs" + sfx, "Custom wheel hub colour", "Used when the wheel hub colour is Custom.", PaletteColor.Gold, v => look.CustomHubs = v, () => hubs.Value == HubColor.Custom);
                ConfigEntry<LanternColor> light = p.Choice("Cart", "LanternColor" + sfx, look.LanternLight, "Lantern light", "", v => look.LanternLight = v, new[] { "Warm (game default)", "White", "Green", "Blue", "Custom" }, look: true);
                p.Color("Cart", "CustomLantern" + sfx, "Custom light", "Used when the lantern light is Custom.", PaletteColor.Cream, v => look.CustomLantern = v, () => light.Value == LanternColor.Custom);
            });
            p.Toggle("Cart", "Lantern", me.Lantern, "Lantern", "The lantern on its pole.", v => me.Lantern = v);
            p.Toggle("Cart", "LanternByDay", me.LanternByDay, "Lantern lit by day", "Off: it goes dark by day, like your hand lantern.", v => me.LanternByDay = v);

            p.Toggle("Sound", "Bell", me.BellOnSetOff, "Bell sound when setting off", "When the cart sets off with a load after standing.", v => me.BellOnSetOff = v);
            p.Toggle("Sound", "Brays", me.Brays, "Bray sounds", "Now and then, and when you get on.", v => me.Brays = v);
            p.Steps("Sound", "Volume", Mathf.RoundToInt(me.SoundVolume * 100f), 0, 150, 10, "Mount and cart sounds", "Hoofsteps, wheels, brays, the bell; on top of the game's SFX volume.", v => me.SoundVolume = v / 100f, v => v + "%");
            p.Publish();
            Logger.LogInfo($"GK2 Saddle Up {Version} loaded (GK2 Donkey API {Mounts.ApiVersion}): ride {rideKey.Value}, cart {cartKey.Value}.");
        }
    }
}
