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
    // Mounts.SettingsPage: the options window, opened from the game's Settings window and the Mounts panel, its keys polled and
    // the mod's tuning cleared if it's unloaded). This mod only lists its settings and what its keys do, so it's also
    // the example of how little a player mod needs. Its built-in content (Gerry) is a mount pack embedded in this DLL
    // (the csproj's assets\mounts\**) and handed to the API in Awake. Every other mount and skin comes from a mod or a
    // pack of its own, registered with the API, never with this mod: it offers whatever the API has (docs/making-a-mount.md
    // "Built in or external"). The API is the backend; a mount is a pack; the API draws it, seats the rider, colours it,
    // hitches its cart.
    [BepInPlugin(Guid, "GK2 Saddle Up", Version)]
    [BepInDependency("dreas.gk2.donkeyapi", "0.40.0")] // at least the API it was written against (NeedsApi below)
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "dreas.gk2.saddleup";
        // This mod's own version; the GK2 Donkey API counts its own (Mounts.ApiVersion), and NeedsApi says which one this
        // mod was written against.
        public const string Version = "0.13.2";

        // The API this mod is written against: 0.40 the Freeplay keys (SpeedBand, Mounts.Catalog.SpeedWords, MostCargo), the per-mount name, babies, voice and lantern (MountLook) and the
        // look Toggle and Text rows; 0.39 reads a mount's own [Jump]; 0.38 has flying for players (the ClimbKey and DescendKey rows); 0.36 CartIndoors (the indoors rows), Mounts.Catalog (the mount
        // rename) and ships no skin; 0.35 the Mounts panel (PanelButton,
        // OpenPanel) and TuningKey (0.34
        // a mount registered in any plugin's Awake and a mod's embedded packs; 0.31 the page's Heading; 0.30 the skins, MountCompanion.Skin;
        // 0.28 CartSlots, 0.27 CorpseIntoMorgue, 0.23 CargoChest, 0.22 CargoZombies). Checked before anything of the API
        // is used, so an older API gets a clear message instead of a missing-method error. Keep it the same as the
        // BepInDependency minimum above.
        private static readonly Version NeedsApi = new Version(0, 40, 0);

        private const string SkinsPrefix = "GK2SaddleUp.mounts."; // the embedded packs' resource names (the csproj: assets\mounts\**)

        private void Awake()
        {
            if (Mounts.ApiVersion < NeedsApi)
            {
                Logger.LogError($"GK2 Donkey API {Mounts.ApiVersion} is too old (this mod needs {NeedsApi} or newer). Update GK2 Donkey API. The mount is off.");
                return;
            }
            // The built-in content: registered before the page is built, so its rows are made with it (the packs' mounts
            // are registered before, at the API's start; any other mod's are caught up the frame after Publish).
            int skins = Mounts.Looks.RegisterEmbedded(Guid, Assembly.GetExecutingAssembly(), SkinsPrefix);
            if (skins > 0) Logger.LogInfo($"GK2 Saddle Up: {skins} built-in skin(s) registered (Gerry).");
            MountCompanion me = Mounts.Companion(Guid);
            Mounts.Behaviour.Set(Guid, "StayMountedThroughDoors", false); // not offered until it's tested
            Mounts.Behaviour.Set(Guid, "FlyIndoors", false); // never offered: no flying inside buildings with Saddle Up (the user, 2026-10-23)
            Mounts.Behaviour.Set(Guid, "HitchKey", KeyCode.None); // H and G switch riding and driving; E hitches a cart in reach

            MountSettingsPage p = Mounts.SettingsPage(Guid, Config, "GK2 Saddle Up", "dreas",
                "Ride your own mount and drive its cart. Press H to ride, G to drive the cart and L to choose your mount. Haul logs, stone and corpses, and find your mount where you left it. Comes with the donkey and Gerry. More mounts and skins can be downloaded.");
            // no Status row since 0.12 (the user, 2026-10-27: "Active" sat in Keys for no reason): the Mods list's
            // Compatibility line and the log say when the mod is off and why
            // the Mounts panel first, on L (the user, 2026-10-27: "we cant not have a default hotkey for this"; L is
            // unbound in the game's defaults, docs/api-design.md "Showcase keys"): where the mount, its skins, name,
            // babies and colours are chosen
            p.Key("Keys", "MountsKey", new KeyboardShortcut(KeyCode.L), "Mounts panel", "Opens the Mounts panel. Pick your mount there and set its name, skin, babies and colours.",
                pressed: p.OpenPanel);
            ConfigEntry<KeyboardShortcut> rideKey = p.Key("Keys", "RideKey", new KeyboardShortcut(KeyCode.H), "Ride your mount", "Calls your mount and gets you on. Press it again to get off. In the cart, it leaves the cart standing and you ride on.",
                pressed: () => me.Use(MountStyle.Ride));
            ConfigEntry<KeyboardShortcut> cartKey = p.Key("Keys", "CartKey", new KeyboardShortcut(KeyCode.G), "Drive the cart", "Calls your mount with its cart so you can drive. Press it again to get off. While riding, it brings the cart.",
                pressed: () => me.Use(MountStyle.Cart));
            p.Key("Keys", "BellKey", new KeyboardShortcut(KeyCode.B), "Ring the bell", "Rings the bell while you drive the cart.", pressed: me.RingBell);
            // the mount's own cry on demand (the user, 2026-10-25): the voice event of the ridden mount, as its idle timer
            // plays it, and its babies answer; V is unbound in the game (docs/api-design.md "Showcase keys")
            p.Key("Keys", "CallOutKey", new KeyboardShortcut(KeyCode.V), "Call out", "Your mount calls out while you ride, and its babies answer.", pressed: Mounts.Bray);
            p.Key("Keys", "SendAwayKey", KeyboardShortcut.Empty, "Send your mount away", "Sends your mount, its cart and its babies away. The cart's load is put down first.",
                pressed: me.SendAway);
            // jumping and flying (on for everyone since API 0.38.0; the rows grey while a dev switched it off). One key for
            // both: a press jumps, a second press in the air takes off on a mount that flies, a press while flying lands;
            // in the air, Climb and Descend are held.
            p.Key("Keys", "JumpKey", new KeyboardShortcut(KeyCode.Space), "Jump", "Your mount jumps while you ride. Hold the key to jump higher. In a fight, Space attacks instead, so set another key if you want to jump while fighting.",
                apply: v => Mounts.Behaviour.Set(Guid, "JumpKey", v));
            p.Key("Keys", "FlyKey", new KeyboardShortcut(KeyCode.Space), "Fly", "Takes off on a mount that can fly. Press it again while flying to land. If it shares the Jump key, jump first and press it in the air to take off. In a fight, Space attacks instead.",
                apply: v => Mounts.Behaviour.Set(Guid, "FlyKey", v));
            p.Key("Keys", "ClimbKey", new KeyboardShortcut(KeyCode.LeftShift), "Climb (hold)", "Hold it while flying to go up.",
                apply: v => Mounts.Behaviour.Set(Guid, "ClimbKey", v));
            p.Key("Keys", "DescendKey", new KeyboardShortcut(KeyCode.LeftControl), "Descend (hold)", "Hold it while flying to go down. You land when you reach the ground.",
                apply: v => Mounts.Behaviour.Set(Guid, "DescendKey", v));
            p.EnableWhen("Keys", "JumpKey", () => Mounts.FlyingEnabled);
            p.EnableWhen("Keys", "FlyKey", () => Mounts.FlyingEnabled);
            p.EnableWhen("Keys", "ClimbKey", () => Mounts.FlyingEnabled);
            p.EnableWhen("Keys", "DescendKey", () => Mounts.FlyingEnabled);

            // The page since 0.11: Keys; Mount (what you have: its picture, its name, what it is and how it is drawn,
            // its babies, whether it is kept with the save); Colours (every colour of it and of the cart); Behaviour (what
            // it does); Cart (what the cart does); Sound. (The Effect section went on 2026-10-25: its keys are retired below.) A row that lived
            // elsewhere in a released version names its old place with MovedKey before it, so a player's value carries
            // over; the call stays for good (docs/compatibility.md). Name and the babies were in Donkey before 0.10;
            // KeepWithSaves in Behaviour, the colours in Donkey and Cart, the cart's rows in Behaviour before 0.11.
            // Moving a section renumbers it in the cfg; the page carries those values over by itself.
            p.MovedKey("Mount", "Name", "Donkey");
            // The babies are the mount's (per mount, in the Mounts panel's journal, 2026-10-26): Babies in 0.12's builds,
            // Mount in 0.10, Donkey before. The voice volume and the lantern are the mount's too: Sound and Cart before.
            p.MovedKey("Mount", "Foals", "Babies");
            p.MovedKey("Mount", "Foals", "Donkey");
            p.MovedKey("Mount", "FoalsIndoors", "Babies");
            p.MovedKey("Mount", "FoalsIndoors", "Donkey");
            p.RetiredKey("Babies", "FoalsWearSkin"); // the row is gone since 0.12 (FoalRows retires it under Mount)
            p.MovedKey("Mount", "FoalsFollow", "Babies");
            p.MovedKey("Mount", "Volume", "Sound");
            p.MovedKey("Colours", "Lantern", "Cart");
            p.MovedKey("Colours", "LanternByDay", "Cart");
            p.MovedKey("Mount", "KeepWithSaves", "Behaviour");
            p.Preview("Mount", me, "Your mount");
            // The mount, its skins and every colour are chosen in the Mounts panel (the user, 2026-10-20: a
            // panel of its own, the mounts listed, the chosen one drawn with its stats and abilities; it stays the
            // same size however many mounts are installed). The rows below are bound as before (the same cfg keys)
            // and marked look: true, which keeps them out of the menu and in the panel.
            p.PanelButton("Mount", "Mounts", "Opens the Mounts panel. Pick your mount there and set its name, skin, babies and colours.");
            // before any mount's own settings are made (each starts from the companion's): on in Saddle Up (the user,
            // 2026-10-22); the API's stays off for other mods
            me.FoalsFollowOnFoot = true;
            p.MountRows("Mount", me);
            // Its name and its voice, per mount, in the Mounts panel's journal under the Skin (the user, 2026-10-26)
            p.PerMount((mountId, sfx, shown) =>
            {
                MountOwn own = me.OwnOf(mountId);
                p.Text("Mount", "Name" + sfx, own.Name ?? "", "Name", "Shown when you walk up to your mount, as in \"Ride Bella\".", v => own.Name = v, look: true);
            });
            p.Toggle("Mount", "KeepWithSaves", me.KeepWithSaves, "Keep your mount with the save", "After you load a game, your mount and cart are where you left them, with their load. They are kept in a small file next to each save, which Steam Cloud syncs.", v => me.KeepWithSaves = v);

            // The babies (a section of their own since 0.12): how many, following you on foot, indoors, the same mount and skin.
            p.FoalRows("Mount", me);
            p.PerMount((mountId, sfx, shown) =>
            {
                MountOwn own = me.OwnOf(mountId);
                // three sliders since 0.12 (the user, 2026-10-27): "Volume" (the one "Voice and steps" row before) is the voices
                // now, so a player's value carries over there; Steps and Baby steps start at 100%
                p.Steps("Mount", "Volume" + sfx, Mathf.RoundToInt(own.SoundVolume * 100f), 0, 150, 10, "Voices volume", "How loud your mount's calls are, and its babies' answers.", v => own.SoundVolume = v / 100f, v => v + "%", look: true);
                p.Steps("Mount", "StepsVolume" + sfx, Mathf.RoundToInt(own.StepsVolume * 100f), 0, 150, 10, "Steps volume", "How loud its steps are, with the cart, the bell and its wings.", v => own.StepsVolume = v / 100f, v => v + "%", look: true);
                p.Steps("Mount", "BabyStepsVolume" + sfx, Mathf.RoundToInt(own.BabyStepsVolume * 100f), 0, 150, 10, "Baby steps volume", "How loud its babies' steps are.", v => own.BabyStepsVolume = v / 100f, v => v + "%", look: true,
                    visible: () => Mounts.Catalog.Find(mountId)?.Foals ?? true);
            });

            // The colours: the button opens them over the game, the rows are here too, under a heading for the mount
            // and one for the cart (not the picture rows' labels: a picture is found on screen by its label). Kept per skin since 0.13 (PerSkin, the user, 2026-10-28:
            // a skin names its own regions; docs/per-skin-looks.md): a mount's first skin keeps the 0.12 keys (the donkey's plain, another mount's Coat_<mount>), every other
            // skin's carry its id (Coat_donkey/<pack>.<skin>); a 0.12 file's values go to the skin that was worn. Coat is the body (the donkey's coat, another mount's
            // scales or skin); Harness its tack, on the mounts whose look has any. A Color row's lightness key moves
            // with its colour key (Color registers it).
            // no picture or pointer line here (2026-10-21): every row of the section is the Mounts panel's, the menus leave the
            // section out; the rows below still make "3. Colours" in the cfg, so players' files keep their section
            p.Heading("Colours", "Mount colours");
            p.PerSkin((skinId, mountId, sfx, shown) =>
            {
                MountLook look = me.LookOf(skinId);
                p.MovedKey("Colours", "Coat" + sfx, "Donkey");
                p.MovedKey("Colours", "CustomCoat" + sfx, "Donkey");
                p.MovedKey("Colours", "Harness" + sfx, "Donkey");
                p.MovedKey("Colours", "CustomHarness" + sfx, "Donkey");
                ConfigEntry<CoatColor> coat = p.Choice("Colours", "Coat" + sfx, look.Coat, "Coat colour", "The colour of your mount's body.", v => look.Coat = v, new[] { "Game default", "Brown", "Dark", "Cream", "Black", "Custom" }, look: true);
                p.Color("Colours", "CustomCoat" + sfx, "Custom coat colour", "Used when the coat colour is Custom.", PaletteColor.Chestnut, v => look.CustomCoat = v, () => coat.Value == CoatColor.Custom);
                ConfigEntry<HarnessColor> harness = p.Choice("Colours", "Harness" + sfx, look.Harness, "Harness colour", "The colour of your mount's harness. Mounts without one ignore it.", v => look.Harness = v, new[] { "Game default", "Black", "Red", "Blue", "Green", "Custom" }, look: true);
                // a skin without tack: the harness rows grey, the value is kept (the chosen skin's regions, an over skin of the donkey keeps the donkey's)
                p.EnableWhen("Colours", "Harness" + sfx, () => MountSettingsPage.SkinHasRegion(skinId, "Harness"));
                p.Color("Colours", "CustomHarness" + sfx, "Custom harness colour", "Used when the harness colour is Custom.", PaletteColor.Wine, v => look.CustomHarness = v, () => harness.Value == HarnessColor.Custom);
                p.EnableWhen("Colours", "CustomHarness" + sfx, () => MountSettingsPage.SkinHasRegion(skinId, "Harness"));
                // the second body tone (the API's Muzzle region): the donkey's muzzle, another mount's belly, shell or tail;
                // a mount whose look has none greys the rows (the user, 2026-10-20:
                // every part of every skin reachable from the page)
                ConfigEntry<MuzzleColor> muzzle = p.Choice("Colours", "Muzzle" + sfx, look.Muzzle, "Second tone", "Your mount's second colour, such as the donkey's muzzle. Mounts without one ignore it.", v => look.Muzzle = v, new[] { "Game default", "Pale", "Rose", "Brown", "Green", "Blue", "Dark", "Custom" }, look: true);
                p.EnableWhen("Colours", "Muzzle" + sfx, () => MountSettingsPage.SkinHasRegion(skinId, "Muzzle"));
                p.Color("Colours", "CustomMuzzle" + sfx, "Custom second tone", "Used when the second tone is Custom.", PaletteColor.Chestnut, v => look.CustomMuzzle = v, () => muzzle.Value == MuzzleColor.Custom);
                p.EnableWhen("Colours", "CustomMuzzle" + sfx, () => MountSettingsPage.SkinHasRegion(skinId, "Muzzle"));
            });
            p.Heading("Colours", "Cart colours");
            p.PerSkin((skinId, mountId, sfx, shown) =>
            {
                MountLook look = me.LookOf(skinId);
                p.MovedKey("Colours", "Paint" + sfx, "Cart");
                p.MovedKey("Colours", "CustomPaint" + sfx, "Cart");
                p.MovedKey("Colours", "Wheels" + sfx, "Cart");
                p.MovedKey("Colours", "CustomWheels" + sfx, "Cart");
                p.MovedKey("Colours", "Hubs" + sfx, "Cart");
                p.MovedKey("Colours", "CustomHubs" + sfx, "Cart");
                p.MovedKey("Colours", "LanternColor" + sfx, "Cart");
                p.MovedKey("Colours", "CustomLantern" + sfx, "Cart");
                ConfigEntry<CartPaint> paint = p.Choice("Colours", "Paint" + sfx, look.Paint, "Paint", "The colour of the cart.", v => look.Paint = v, new[] { "Game default", "Dark wood", "Red", "Green", "Blue", "Custom" }, look: true);
                p.Color("Colours", "CustomPaint" + sfx, "Custom paint", "Used when the paint is Custom.", PaletteColor.Brick, v => look.CustomPaint = v, () => paint.Value == CartPaint.Custom);
                ConfigEntry<WheelColor> wheels = p.Choice("Colours", "Wheels" + sfx, look.Wheels, "Wheel colour", "The colour of the wheels.", v => look.Wheels = v, new[] { "Game default", "Dark wood", "Red", "Green", "Blue", "Black", "Custom" }, look: true);
                p.Color("Colours", "CustomWheels" + sfx, "Custom wheel colour", "Used when the wheel colour is Custom.", PaletteColor.Mustard, v => look.CustomWheels = v, () => wheels.Value == WheelColor.Custom);
                ConfigEntry<HubColor> hubs = p.Choice("Colours", "Hubs" + sfx, look.Hubs, "Wheel hub colour", "The colour of the metal in the middle of the wheels.", v => look.Hubs = v, new[] { "Game default", "Black", "Brass", "Silver", "Custom" }, look: true);
                p.Color("Colours", "CustomHubs" + sfx, "Custom wheel hub colour", "Used when the wheel hub colour is Custom.", PaletteColor.Gold, v => look.CustomHubs = v, () => hubs.Value == HubColor.Custom);
                ConfigEntry<LanternColor> light = p.Choice("Colours", "LanternColor" + sfx, look.LanternLight, "Lantern light", "The colour of the lantern's light.", v => look.LanternLight = v, new[] { "Warm (game default)", "White", "Green", "Blue", "Custom" }, look: true);
                p.Color("Colours", "CustomLantern" + sfx, "Custom light", "Used when the lantern light is Custom.", PaletteColor.Cream, v => look.CustomLantern = v, () => light.Value == LanternColor.Custom);
                // the lantern itself, per skin with its light (the user, 2026-10-26: from the Cart options into the colours window; 2026-10-28: part of the look)
                p.Toggle("Colours", "Lantern" + sfx, look.Lantern, "Lantern", "Shows the lantern on its pole.", v => look.Lantern = v, look: true);
                p.Toggle("Colours", "LanternByDay" + sfx, look.LanternByDay, "Lantern lit by day", "Keeps the lantern lit in daylight. Turn it off to light it only at night.", v => look.LanternByDay = v, look: true);
                // a mount that can't pull the cart has no cart colours (the user, 2026-10-25); the values are kept
                foreach (string key in new[] { "Paint", "CustomPaint", "Wheels", "CustomWheels", "Hubs", "CustomHubs", "LanternColor", "CustomLantern", "Lantern", "LanternByDay" }) p.CartRow("Colours", key + sfx);
            });

            // The Effect section (0.10.0 to 0.12: the effect, when it shows, and four toggles) went on 2026-10-25 (the user:
            // "remove all the stuff we're exposing regarding the effects"; how a skin comes with its effect is being
            // designed): a 0.10.0 player's file still has the six lines, which leave it.
            p.RetiredKey("Effect", "Effect");
            p.RetiredKey("Effect", "EffectWhen");
            p.RetiredKey("Effect", "EffectTrail");
            p.RetiredKey("Effect", "EffectWithCart");
            p.RetiredKey("Effect", "EffectInFights");
            p.RetiredKey("Effect", "EffectOnFoals");

            // Riding speed and Cart speed were rows here until 0.12: each mount has its own speed now ([Mount] speed; the
            // cart's follows it; the Mounts panel shows it), so the player's choice is dropped (the user, 2026-10-22) and the
            // old lines leave the file.
            p.RetiredKey("Behaviour", "RideSpeed");
            p.RetiredKey("Behaviour", "CartSpeed");
            // the section is "Mount Behaviour" since 0.12 (the user, 2026-10-27), "Behaviour" before: its values carry over
            p.MovedKey("Mount Behaviour", "ShowMessages", "Behaviour");
            p.MovedKey("Mount Behaviour", "IdleShuffle", "Behaviour");
            p.MovedKey("Mount Behaviour", "MountIndoors", "Behaviour");
            p.MovedKey("Mount Behaviour", "CartIndoors", "Behaviour");
            p.Toggle("Mount Behaviour", "ShowMessages", true, "Say why you can't get on", "The keeper says why in a thought bubble.", v => Mounts.Behaviour.Set(Guid, "ShowMessages", v));
            p.Toggle("Mount Behaviour", "IdleShuffle", true, "Shuffles while standing", "Your mount takes a few slow steps now and then while it waits without its cart.", v => Mounts.Behaviour.Set(Guid, "IdleShuffle", v));
            // Indoors (a player asked, 2026-10-21): both off = as before, the ride ends at a building's door. The API's
            // DismountIndoors is the first row turned round; CartIndoors only counts with it on, so its row greys.
            ConfigEntry<bool> indoors = p.Toggle("Mount Behaviour", "MountIndoors", false, "Allow mounting indoors", "Ride into houses, the church and the morgue. Doors with a loading screen still end the ride.", v => Mounts.Behaviour.Set(Guid, "DismountIndoors", !v));
            p.Toggle("Mount Behaviour", "CartIndoors", false, "Allow mounting with the cart indoors", "The cart comes inside too. It has no collider indoors, so it rolls through furniture and door frames rather than getting stuck; it only looks odd. When this is off, the cart waits at the door.", v => Mounts.Behaviour.Set(Guid, "CartIndoors", v));
            p.EnableWhen("Mount Behaviour", "CartIndoors", () => indoors.Value);

            // Cart capacity was a row here until 0.12: each mount carries its own cargo now ([Mount] cargo; the Mounts panel
            // shows it), so the player's number is dropped (the user, 2026-10-27: a player's old 2 kept every cart at two)
            // and the old line leaves the file, wherever it stood.
            p.RetiredKey("Cart", "CartSlots");
            p.RetiredKey("Behaviour", "CartSlots");
            p.MovedKey("Cart", "CargoZombies", "Behaviour");
            p.MovedKey("Cart", "CargoChest", "Behaviour");
            p.MovedKey("Cart", "CorpseIntoMorgue", "Behaviour");
            p.MovedKey("Cart", "CorpsesAtCrossroads", "Behaviour");
            p.Toggle("Cart", "CargoZombies", true, "Zombies ride on the cart", "Zombies you pick up while driving lie on the cart. You can hand them to their work from there.", v => Mounts.Behaviour.Set(Guid, "CargoZombies", v));
            p.Toggle("Cart", "CargoChest", true, "A storage chest rides on the cart", "Drive up to a storage chest and press the action key to load it with everything inside. Press interact with nothing in front of you to set it down.", v => Mounts.Behaviour.Set(Guid, "CargoChest", v));
            p.Toggle("Cart", "CorpseIntoMorgue", true, "Carry the corpse into the morgue", "Drive through the morgue door with a corpse on the cart and you walk in carrying it. The cart waits outside.", v => Mounts.Behaviour.Set(Guid, "CorpseIntoMorgue", v));
            p.Toggle("Cart", "CorpsesAtCrossroads", false, "Donkey leaves corpses at the crossroads", "The game's delivery donkey leaves new bodies at the crossroads instead of in front of the morgue. You bring them in yourself.", v => Mounts.Corpses.Set(Guid, "CorpsesAtCrossroads", v));
            // Two rows gone in 0.12 (the user, 2026-10-27: "i dont think we need them"): calling your mount leaves a parked
            // cart where it is (CartStaysParked, the companion's default, on) and the cart rings no bell when it sets off
            // (BellOnSetOff, off). The old lines leave the file wherever they stood.
            p.RetiredKey("Cart", "CartStaysParked");
            p.RetiredKey("Behaviour", "CartStaysParked");
            p.RetiredKey("Cart", "Bell");
            p.RetiredKey("Sound", "Bell");

            p.Toggle("Sound", "Brays", me.Brays, "Mount calls", "Your mount calls out now and then, and when you get on.", v => me.Brays = v);
            p.TuningKey("Sound", "Brays", "BrayOnMountChance", me);
            p.Toggle("Sound", "StandingSounds", true, "Standing mounts and babies sound", "Parked mounts and babies near you make their sounds too. When this is off, you only hear the mount you ride.", v => Mounts.Behaviour.Set(Guid, "StandingSounds", v));

            // Never lost (the API's safe spots, docs/never-lost.md), a section of its own at the end (the user, 2026-10-27;
            // before, the key sat in Keys and the rest in Behaviour, never released there): the fall watchdog, the same by
            // hand (a button and a key; the API reads the key with its own gate: not during a door, a cutscene or a
            // dialogue). Where it would put you is in the log, not on the page (the user, 2026-10-27: coordinates mean nothing to a player).
            p.Toggle("Safe spot", "FallGuard", true, "Bring me back if I fall out of the world", "If you fall out of the world, you are put back on the last ground you stood on.", v => Mounts.Behaviour.Set(Guid, "FallGuard", v));
            p.Key("Safe spot", "ReturnKey", KeyboardShortcut.Empty, "Back to the last safe spot", "Puts you back on the last ground you stood on. Use it if you get stuck.",
                apply: v => Mounts.Behaviour.Set(Guid, "ReturnKey", v));
            p.Button("Safe spot", "Back to the last safe spot", "Puts you back on the last ground you stood on, right now.", () =>
            {
                Mounts.ReturnToSafeSpot("Saddle Up's settings button"); // the API says the result in the keeper's bubble
            });

            // Freeplay, the page's last section (the user, 2026-10-27: "for players who dont want the gameplay stuff"):
            // every mount the most cargo, every mount one speed. The speed's words and the most cargo are the API's (the
            // Mounts panel's words, Mounts.Catalog.SpeedWords), never repeated here.
            var speedWords = new string[Mounts.Catalog.SpeedWords.Count];
            for (int i = 0; i < speedWords.Length; i++) speedWords[i] = Mounts.Catalog.SpeedWords[i];
            p.Toggle("Freeplay", "MaxCargo", false, "All mounts carry max cargo", $"Every mount that pulls a cart carries {Mounts.Catalog.MostCargo} items, whatever its own cargo.", v => Mounts.Behaviour.Set(Guid, "FreeplayMaxCargo", v));
            ConfigEntry<bool> sameSpeed = p.Toggle("Freeplay", "SameSpeed", false, "All mounts have the same speed", "Every mount, the donkey too, goes at the speed you pick below. With the cart it is slower, as always.", v => Mounts.Behaviour.Set(Guid, "FreeplaySameSpeed", v));
            p.Choice("Freeplay", "Speed", SpeedBand.Normal, "Speed of every mount", "Normal is the donkey's speed.", v => Mounts.Behaviour.Set(Guid, "FreeplaySpeed", v), speedWords, visible: () => sameSpeed.Value);
            p.Publish();
            Logger.LogInfo($"GK2 Saddle Up {Version} loaded (GK2 Donkey API {Mounts.ApiVersion}): ride {rideKey.Value}, cart {cartKey.Value}.");
        }
    }
}
