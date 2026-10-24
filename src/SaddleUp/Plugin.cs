using System;
using System.Reflection;
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
    // the example of how little a player mod needs. Its five mounts (the Red Dragon, the Giant Rat, the Giant Snail,
    // the Giant Worm, Gerry) are mount packs embedded in this DLL (the csproj's assets\mounts\**: each mount's
    // mount.cfg and sounds\, its skins under skins\; the donkey's Hellfire and Royal Parade under donkey\skins\) and handed to the API
    // in Awake: the API is the backend, this mod ships the content, exactly as anyone else's mod ships a mount
    // (docs/mounts-as-mods.md: a mount is a pack; the API draws it, seats the rider, colours it, hitches its cart).
    [BepInPlugin(Guid, "GK2 Saddle Up", Version)]
    [BepInDependency("dreas.gk2.donkeyapi", "0.36.0")] // at least the API it was written against (NeedsApi below)
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "dreas.gk2.saddleup";
        // This mod's own version; the GK2 Donkey API counts its own (Mounts.ApiVersion), and NeedsApi says which one this
        // mod was written against.
        public const string Version = "0.12.0";

        // The API this mod is written against: 0.36 has CartIndoors (the indoors rows), Mounts.Catalog (the mount
        // rename) and ships no skin (Hellfire's effect is registered here); 0.35 the Mounts panel (PanelButton,
        // OpenPanel) and TuningKey (0.34
        // a mount registered in any plugin's Awake and a mod's embedded packs; 0.31 the page's Heading; 0.30 the skins, MountCompanion.Skin;
        // 0.28 CartSlots, 0.27 CorpseIntoMorgue, 0.23 CargoChest, 0.22 CargoZombies). Checked before anything of the API
        // is used, so an older API gets a clear message instead of a missing-method error. Keep it the same as the
        // BepInDependency minimum above.
        private static readonly Version NeedsApi = new Version(0, 36, 0);

        private const string SkinsPrefix = "GK2SaddleUp.mounts."; // the embedded packs' resource names (the csproj: assets\mounts\**)

        // Hellfire: the lamps' flame on a ring of six and their sparks on three of those spots, a flame left behind. The
        // API keeps it out with the mounts off (this version), as every registration.
        private void RegisterHellfire()
        {
            MountEffects api = Mounts.Effects;
            var d = new MountEffectDef { Id = "hellfire", Name = "Hellfire", Author = "dreas", Version = "1.0" };
            var flame = new MountEffectPart { Source = api.GameParticles("flame_fx", "Assets/AddressableAssets/Materials/flame_fx.mat") };
            flame.Emitters.Add(new MountEffectEmitter { Placement = MountEffectPlacement.Ring, Count = 6, Radius = 0.8f, Size = 1.5f, Height = 0.05f });
            flame.Emitters.Add(new MountEffectEmitter { Placement = MountEffectPlacement.Trail, Size = 1.2f, Height = 0.05f, Every = 0.5f, Burst = 0.4f });
            var sparks = new MountEffectPart { Source = api.GameParticles("sparks_fx", "Assets/AddressableAssets/Materials/sparks_fx.mat") };
            sparks.Emitters.Add(new MountEffectEmitter { Placement = MountEffectPlacement.Ring, Count = 3, Radius = 0.8f, Size = 1.5f, Height = 0.05f });
            d.Parts.Add(flame);
            d.Parts.Add(sparks);
            api.Register(Guid, d);
        }

        private void Awake()
        {
            if (Mounts.ApiVersion < NeedsApi)
            {
                Logger.LogError($"GK2 Donkey API {Mounts.ApiVersion} is too old (this mod needs {NeedsApi} or newer). Update GK2 Donkey API. The mount is off.");
                return;
            }
            // The five mounts: registered before the page is built, so their rows are made with it (any other mod's
            // mounts are caught up the frame after Publish). With the mounts off for players (Mounts.MountsEnabled,
            // this version) the API keeps them out and says so, and the donkey stays the only mount.
            int skins = Mounts.Looks.RegisterEmbedded(Guid, Assembly.GetExecutingAssembly(), SkinsPrefix);
            if (skins > 0) Logger.LogInfo($"GK2 Saddle Up: {skins} skin pack(s) registered (the five mounts: the Red Dragon, the Giant Rat, the Giant Snail, the Giant Worm, Gerry; the donkey's Hellfire and Royal Parade).");
            RegisterHellfire(); // the effect Hellfire comes with: Saddle Up's too, defined through the API as any mod's
            MountCompanion me = Mounts.Companion(Guid);
            Mounts.Behaviour.Set(Guid, "StayMountedThroughDoors", false); // not offered until it's tested
            Mounts.Behaviour.Set(Guid, "FlyIndoors", false); // never offered: no flying inside buildings with Saddle Up (the user, 2026-10-23)
            Mounts.Behaviour.Set(Guid, "HitchKey", KeyCode.None); // H and G switch riding and driving; E hitches a cart in reach

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
            // jumping and flying (the Donkey API's experiment, dev only for now: the rows show only while it is on). One key for
            // both: a press jumps, a second press in the air takes off on a mount that flies, a press while flying lands.
            p.Key("Keys", "JumpKey", new KeyboardShortcut(KeyCode.Space), "Jump", "Riding, your mount jumps: hold the key for a higher jump. Over what is lower than the jump. Not in a fight, where Space attacks. None = no jumping.",
                apply: v => Mounts.Behaviour.Set(Guid, "JumpKey", v));
            p.Key("Keys", "FlyKey", new KeyboardShortcut(KeyCode.Space), "Fly", "Riding a mount that flies, outdoors: take off, and land again. The same key as Jump: jump, then press again in the air to take off. None = no flying.",
                apply: v => Mounts.Behaviour.Set(Guid, "FlyKey", v));
            p.EnableWhen("Keys", "JumpKey", () => Mounts.FlyingEnabled);
            p.EnableWhen("Keys", "FlyKey", () => Mounts.FlyingEnabled);
            // never lost (the API's safe spots, docs/never-lost.md): a key for "back to the last safe spot"; the API reads it
            // with its own gate (not during a door, a cutscene or a dialogue)
            p.Key("Keys", "ReturnKey", KeyboardShortcut.Empty, "Back to the last safe spot", "Stuck, or fallen somewhere you shouldn't be: puts you back on the last ground you stood on in this scene. None = no key (the Behaviour section has a button).",
                apply: v => Mounts.Behaviour.Set(Guid, "ReturnKey", v));
            p.Key("Keys", "MountsKey", KeyboardShortcut.Empty, "Mounts panel", "Opens the Mounts panel over the game: every mount you can ride, the chosen one drawn with what it is and does, its skins, effect and colours. None = no key (the Mount section's button opens it too).",
                pressed: p.OpenPanel);

            // The page since 0.11: Keys; Mount (what you have: its picture, its name, what it is and how it is drawn,
            // its babies, whether it is kept with the save); Colours (every colour of it and of the cart); Effect (the
            // particle effect on it); Behaviour (what it does); Cart (what the cart does); Sound. A row that lived
            // elsewhere in a released version names its old place with MovedKey before it, so a player's value carries
            // over; the call stays for good (docs/compatibility.md). Name and the babies were in Donkey before 0.10;
            // KeepWithSaves in Behaviour, the colours in Donkey and Cart, the cart's rows in Behaviour before 0.11.
            // Moving a section renumbers it in the cfg; the page carries those values over by itself.
            p.MovedKey("Mount", "Name", "Donkey");
            // The babies have a section of their own since 0.12 (the user, 2026-10-22): Mount from 0.10, Donkey before.
            p.MovedKey("Babies", "Foals", "Mount");
            p.MovedKey("Babies", "Foals", "Donkey");
            p.MovedKey("Babies", "FoalsIndoors", "Mount");
            p.MovedKey("Babies", "FoalsIndoors", "Donkey");
            p.MovedKey("Babies", "FoalsWearSkin", "Mount");
            p.MovedKey("Mount", "KeepWithSaves", "Behaviour");
            p.Preview("Mount", me, "Your mount");
            // The mount, its skins, the effect and every colour are chosen in the Mounts panel (the user, 2026-10-20: a
            // panel of its own, the mounts listed, the chosen one drawn with its stats and abilities; it stays the
            // same size however many mounts are installed). The rows below are bound as before (the same cfg keys)
            // and marked look: true, which keeps them out of the menu and in the panel.
            p.PanelButton("Mount", "Mounts", "Closes this and opens the Mounts panel over the game: every mount you can ride, the chosen one drawn with what it is and does, its skins, its effect and its colours. Done keeps your choices, Cancel puts them back.");
            p.Text("Mount", "Name", me.Name ?? "", "Name", "Shown when you walk up to it: \"[E] Ride Bella\". Empty: no name.", v => me.Name = v);
            p.MountRows("Mount", me);
            if (!Mounts.MountsEnabled)
            {
                // This version ships the mounts, skins and effects switched off (Mounts.MountsEnabled; the next update
                // switches them on): the rows above hold the donkey alone, and these say why.
                p.Info("Mount", "Other mounts and skins", () => "Coming in the next update",
                    "A dragon, a giant rat, a giant snail, a giant worm, Gerry, the Hellfire skin, skin packs and the mounts from other mods are built and switched off in this version; the next update switches them on. Until then your mount is the donkey, as before.");
            }
            p.Toggle("Mount", "KeepWithSaves", me.KeepWithSaves, "Keep your mount with the save", "After loading, your mount and cart are where you left them, the load on the cart. They're kept in a small file next to each save (<save>.gk2donkeyapi.dat), which Steam Cloud syncs along with your saves.", v => me.KeepWithSaves = v);

            // The babies (a section of their own since 0.12): how many, following you on foot, indoors, the same mount and skin.
            me.FoalsFollowOnFoot = true; // the row's default: on in Saddle Up (the user, 2026-10-22); the API's stays off for other mods
            p.FoalRows("Babies", me);

            // The colours: the button opens them over the game, the rows are here too, under a heading for the mount
            // and one for the cart (not the picture rows' labels: a picture is found on screen by its label). Kept per mount (PerMount: the donkey's rows keep their keys, the dragon's are
            // Coat_dragon and so on; docs/mount-colours.md). Coat is the body (the donkey's coat, a dragon's scales,
            // the worm's skin); Harness its tack, on the mounts whose look has any. A Color row's lightness key moves
            // with its colour key (Color registers it).
            // no picture or pointer line here (2026-10-21): every row of the section is the Mounts panel's, the menus leave the
            // section out; the rows below still make "3. Colours" in the cfg, so players' files keep their section
            p.Heading("Colours", "Mount colours");
            p.PerMount((mountId, sfx, shown) =>
            {
                MountLook look = me.LookOf(mountId);
                p.MovedKey("Colours", "Coat" + sfx, "Donkey");
                p.MovedKey("Colours", "CustomCoat" + sfx, "Donkey");
                p.MovedKey("Colours", "Harness" + sfx, "Donkey");
                p.MovedKey("Colours", "CustomHarness" + sfx, "Donkey");
                ConfigEntry<CoatColor> coat = p.Choice("Colours", "Coat" + sfx, look.Coat, "Coat colour", "The mount's body: the donkey's coat, a dragon's scales, the worm's skin. Kept per mount.", v => look.Coat = v, new[] { "Game default", "Brown", "Dark", "Cream", "Black", "Custom" }, look: true);
                p.Color("Colours", "CustomCoat" + sfx, "Custom coat colour", "Used when the coat colour is Custom.", PaletteColor.Chestnut, v => look.CustomCoat = v, () => coat.Value == CoatColor.Custom);
                ConfigEntry<HarnessColor> harness = p.Choice("Colours", "Harness" + sfx, look.Harness, "Harness colour", "The mount's tack (the donkey's harness), on the mounts whose look has any; the others ignore it. Kept per mount.", v => look.Harness = v, new[] { "Game default", "Black", "Red", "Blue", "Green", "Custom" }, look: true);
                // a mount whose skins have no tack (the worm): the harness rows grey, the value is kept
                p.EnableWhen("Colours", "Harness" + sfx, () => MountSettingsPage.MountHasRegion(mountId, "Harness"));
                p.Color("Colours", "CustomHarness" + sfx, "Custom harness colour", "Used when the harness colour is Custom.", PaletteColor.Wine, v => look.CustomHarness = v, () => harness.Value == HarnessColor.Custom);
                p.EnableWhen("Colours", "CustomHarness" + sfx, () => MountSettingsPage.MountHasRegion(mountId, "Harness"));
                // the second body tone (the API's Muzzle region): the donkey's muzzle, the rat's tail, ears and nose, the
                // snail's shell, the dragon's belly; a mount whose look has none (the worm) greys the rows (the user, 2026-10-20:
                // every part of every skin reachable from the page)
                ConfigEntry<MuzzleColor> muzzle = p.Choice("Colours", "Muzzle" + sfx, look.Muzzle, "Second tone", "The mount's second body tone, where its look has one: the donkey's muzzle, the rat's tail, ears and nose, the snail's shell, the dragon's belly, Hellfire's lava; the others ignore it. Kept per mount.", v => look.Muzzle = v, new[] { "Game default", "Pale", "Rose", "Brown", "Green", "Blue", "Dark", "Custom" }, look: true);
                p.EnableWhen("Colours", "Muzzle" + sfx, () => MountSettingsPage.MountHasRegion(mountId, "Muzzle"));
                p.Color("Colours", "CustomMuzzle" + sfx, "Custom second tone", "Used when the second tone is Custom.", PaletteColor.Chestnut, v => look.CustomMuzzle = v, () => muzzle.Value == MuzzleColor.Custom);
                p.EnableWhen("Colours", "CustomMuzzle" + sfx, () => MountSettingsPage.MountHasRegion(mountId, "Muzzle"));
            });
            p.Heading("Colours", "Cart colours");
            p.PerMount((mountId, sfx, shown) =>
            {
                MountLook look = me.LookOf(mountId);
                p.MovedKey("Colours", "Paint" + sfx, "Cart");
                p.MovedKey("Colours", "CustomPaint" + sfx, "Cart");
                p.MovedKey("Colours", "Wheels" + sfx, "Cart");
                p.MovedKey("Colours", "CustomWheels" + sfx, "Cart");
                p.MovedKey("Colours", "Hubs" + sfx, "Cart");
                p.MovedKey("Colours", "CustomHubs" + sfx, "Cart");
                p.MovedKey("Colours", "LanternColor" + sfx, "Cart");
                p.MovedKey("Colours", "CustomLantern" + sfx, "Cart");
                ConfigEntry<CartPaint> paint = p.Choice("Colours", "Paint" + sfx, look.Paint, "Paint", "The cart's paint with this mount.", v => look.Paint = v, new[] { "Game default", "Dark wood", "Red", "Green", "Blue", "Custom" }, look: true);
                p.Color("Colours", "CustomPaint" + sfx, "Custom paint", "Used when the paint is Custom.", PaletteColor.Brick, v => look.CustomPaint = v, () => paint.Value == CartPaint.Custom);
                ConfigEntry<WheelColor> wheels = p.Choice("Colours", "Wheels" + sfx, look.Wheels, "Wheel colour", "The wood of the wheels.", v => look.Wheels = v, new[] { "Game default", "Dark wood", "Red", "Green", "Blue", "Black", "Custom" }, look: true);
                p.Color("Colours", "CustomWheels" + sfx, "Custom wheel colour", "Used when the wheel colour is Custom.", PaletteColor.Mustard, v => look.CustomWheels = v, () => wheels.Value == WheelColor.Custom);
                ConfigEntry<HubColor> hubs = p.Choice("Colours", "Hubs" + sfx, look.Hubs, "Wheel hub colour", "The iron in the middle of the wheels.", v => look.Hubs = v, new[] { "Game default", "Black", "Brass", "Silver", "Custom" }, look: true);
                p.Color("Colours", "CustomHubs" + sfx, "Custom wheel hub colour", "Used when the wheel hub colour is Custom.", PaletteColor.Gold, v => look.CustomHubs = v, () => hubs.Value == HubColor.Custom);
                ConfigEntry<LanternColor> light = p.Choice("Colours", "LanternColor" + sfx, look.LanternLight, "Lantern light", "", v => look.LanternLight = v, new[] { "Warm (game default)", "White", "Green", "Blue", "Custom" }, look: true);
                p.Color("Colours", "CustomLantern" + sfx, "Custom light", "Used when the lantern light is Custom.", PaletteColor.Cream, v => look.CustomLantern = v, () => light.Value == LanternColor.Custom);
            });

            if (!Mounts.MountsEnabled)
            {
                p.Info("Effect", "Particle effects", () => "Coming in the next update",
                    "Hellfire (the game's lamp fire in a ring round your mount, with sparks and a trail) and effects from mods are built and switched off in this version; the next update switches them on.");
            }
            p.EffectRows("Effect", me);

            // Riding speed and Cart speed were rows here until 0.12: each mount has its own speed now ([Mount] speed; the
            // cart's follows it; the Mounts panel shows it), so the player's choice is dropped (the user, 2026-10-22) and the
            // old lines leave the file.
            p.RetiredKey("Behaviour", "RideSpeed");
            p.RetiredKey("Behaviour", "CartSpeed");
            p.Toggle("Behaviour", "ShowMessages", true, "Say why mounting is refused", "The keeper's thought bubble.", v => Mounts.Behaviour.Set(Guid, "ShowMessages", v));
            p.Toggle("Behaviour", "IdleShuffle", true, "Shuffles while standing", "Left without its cart, it takes a few slow steps now and then.", v => Mounts.Behaviour.Set(Guid, "IdleShuffle", v));
            // Indoors (a player asked, 2026-10-21): both off = as before, the ride ends at a building's door. The API's
            // DismountIndoors is the first row turned round; CartIndoors only counts with it on, so its row greys.
            ConfigEntry<bool> indoors = p.Toggle("Behaviour", "MountIndoors", false, "Allow mounting indoors", "Ride into houses, the church and the morgue, and get on inside. A door with a loading screen still ends the ride. Off: you get off at the door and can't mount inside.", v => Mounts.Behaviour.Set(Guid, "DismountIndoors", !v));
            p.Toggle("Behaviour", "CartIndoors", false, "Allow mounting with the cart indoors", "With mounting indoors allowed: the cart comes inside too (it can get caught on furniture and door frames). Off: driving the cart through a door, the cart stays at the door with its load and you ride on inside (at the morgue you get off inside carrying the corpse, your mount with you); inside you can ride, not drive.", v => Mounts.Behaviour.Set(Guid, "CartIndoors", v));
            p.EnableWhen("Behaviour", "CartIndoors", () => indoors.Value);
            // never lost: the fall watchdog, and the same by hand
            p.Toggle("Behaviour", "FallGuard", true, "Bring me back if I fall out of the world", "If you ever end up under the ground, falling into nothing or far below the world, you are put back on the last ground you stood on (the log says why). Never during a door, a cutscene or a dialogue; standing under a bridge is fine.", v => Mounts.Behaviour.Set(Guid, "FallGuard", v));
            p.Button("Behaviour", "Back to the last safe spot", "Puts you back on the last ground you stood on in this scene, right now (stuck somewhere, or the watchdog is off). The Keys section has a key for it.", () =>
            {
                Mounts.ReturnToSafeSpot("Saddle Up's settings button"); // the API says the result in the keeper's bubble
            });
            p.Info("Behaviour", "Last safe spot", () => Mounts.LastSafeSpot is Vector3 spot ? $"{spot.x:0.0}, {spot.z:0.0} (height {spot.y:0.0})" : "none yet in this scene", "Where \"Back to the last safe spot\" would put you.");

            p.MovedKey("Cart", "CartSlots", "Behaviour");
            p.MovedKey("Cart", "CargoZombies", "Behaviour");
            p.MovedKey("Cart", "CargoChest", "Behaviour");
            p.MovedKey("Cart", "CorpseIntoMorgue", "Behaviour");
            p.MovedKey("Cart", "CorpsesAtCrossroads", "Behaviour");
            p.MovedKey("Cart", "CartStaysParked", "Behaviour");
            p.Steps("Cart", "CartSlots", 6, 2, 6, 2, "Cart capacity", "How many items the cart carries: 6 (three layers of two), 4 (two layers) or 2 like the game's own delivery cart. Piles, tables and stations take from all of them; a chest aboard still takes the whole cart. Lowering it never drops anything: a fuller cart stays full until unloaded.", v => Mounts.Behaviour.Set(Guid, "CartSlots", v), v => v + " items");
            p.Toggle("Cart", "CargoZombies", true, "Zombies ride on the cart", "Pick up a zombie while driving and it lies on the cart, as many as the cart holds; hand it to its work straight from the cart. Off: the cart refuses zombies.", v => Mounts.Behaviour.Set(Guid, "CargoZombies", v));
            p.Toggle("Cart", "CargoChest", true, "A storage chest rides on the cart", "Drive up to a plain storage chest (rough, normal or good) and press the action key: it rides on the cart with everything inside, alone. The interact key with nothing in front sets it down. Off: chests stay where they are.", v => Mounts.Behaviour.Set(Guid, "CargoChest", v));
            p.Toggle("Cart", "CorpseIntoMorgue", true, "Carry the corpse into the morgue", "Driving the cart through the morgue door with a corpse on it: you get off at the door as always and the cart stays outside with its load, but you walk in carrying the corpse (riding indoors allowed and the cart not: your mount comes in with you). Off: it stays on the cart.", v => Mounts.Behaviour.Set(Guid, "CorpseIntoMorgue", v));
            p.Toggle("Cart", "CorpsesAtCrossroads", false, "Donkey leaves corpses at the crossroads", "The game's delivery donkey leaves the bodies it brings at the crossroads by the nun, where it appears, instead of putting them on the morgue pallets. You carry them to the morgue yourself, or cart them. Off: the game's own delivery.", v => Mounts.Corpses.Set(Guid, "CorpsesAtCrossroads", v));
            p.Toggle("Cart", "CartStaysParked", me.CartStaysParked, "Calling your mount leaves the cart parked", "Ride key while your mount stands somewhere with the cart: it comes alone and the cart stays where you left it, with its load. Off: both come and the cart is left standing next to you.", v => me.CartStaysParked = v);
            p.Toggle("Cart", "Lantern", me.Lantern, "Lantern", "The lantern on its pole.", v => me.Lantern = v);
            p.Toggle("Cart", "LanternByDay", me.LanternByDay, "Lantern lit by day", "Off: it goes dark by day, like your hand lantern.", v => me.LanternByDay = v);

            p.Toggle("Sound", "Bell", me.BellOnSetOff, "Bell sound when setting off", "When the cart sets off with a load after standing.", v => me.BellOnSetOff = v);
            p.Toggle("Sound", "Brays", me.Brays, "Bray sounds", "Now and then, and when you get on.", v => me.Brays = v);
            p.TuningKey("Sound", "Bell", "BellOnSetOff", me);
            p.TuningKey("Sound", "Brays", "BrayOnMountChance", me);
            p.Steps("Sound", "Volume", Mathf.RoundToInt(me.SoundVolume * 100f), 0, 150, 10, "Mount and cart sounds", "Hoofsteps, wheels, brays, the bell; on top of the game's SFX volume.", v => me.SoundVolume = v / 100f, v => v + "%");
            p.Toggle("Sound", "StandingSounds", true, "Standing mounts and babies sound", "Parked mounts and babies near you make their sounds: steps, grazing, the lip smack, a call now and then, the babies answering their parent. Off: only the mount you ride.", v => Mounts.Behaviour.Set(Guid, "StandingSounds", v));
            p.Publish();
            Logger.LogInfo($"GK2 Saddle Up {Version} loaded (GK2 Donkey API {Mounts.ApiVersion}): ride {rideKey.Value}, cart {cartKey.Value}.");
        }
    }
}
