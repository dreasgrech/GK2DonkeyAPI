using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using GK2DonkeyAPI.Api;
using UnityEngine;

namespace SaddleUp
{
    // The player mod: the keeper's own donkey, cart and foal, built only on the public GK2 Donkey API (docs/api.md).
    // The API does the work (Mounts.Companion: calling, riding, hitching, looks, keeping it with the save; and
    // Mounts.SettingsPage: the Mods menu with GK2 Mod Framework, else the game's own Settings window). This mod only
    // defines its settings and maps its keys, so it's also the example of how little a player mod needs.
    [BepInPlugin(Guid, "GK2 Saddle Up!", Version)]
    [BepInDependency("dreas.gk2.donkeyapi", "0.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "dreas.gk2.saddleup";
        public const string Version = "0.1.0";

        // The API this mod is written against: 0.14 has the companion and the settings page.
        private static readonly Version NeedsApi = new Version(0, 14, 0);

        internal static ManualLogSource Log;
        private ConfigEntry<KeyboardShortcut> rideKey, cartKey, bellKey, sendAwayKey;
        private MountCompanion me;
        private string status = "not started";

        private void Awake()
        {
            Log = Logger;
            if (Mounts.ApiVersion < NeedsApi)
            {
                Log.LogError($"GK2 Donkey API {Mounts.ApiVersion} is too old (this mod needs {NeedsApi} or newer). Update GK2 Donkey API. The mount is off.");
                return;
            }
            me = Mounts.Companion(Guid);
            status = Mounts.IsAvailable ? "Active" : "Off: GK2 Donkey API is off (its General.Enabled, or it switched itself off after a game update; see BepInEx/LogOutput.log).";
            Mounts.Behaviour.Set(Guid, "StayMountedThroughDoors", false); // not offered until it's tested

            MountSettingsPage p = Mounts.SettingsPage(Guid, Config, "GK2 Saddle Up!", "dreas",
                "Your own donkey and cart: ride it (H), drive it (G), haul with the cart. Built on GK2 Donkey API.", () => status);
            p.Info("Keys", "Status", () => status, "What the mod is doing right now.");
            rideKey = p.Key("Keys", "RideKey", new KeyboardShortcut(KeyCode.H), "Ride the donkey", "Your donkey comes and you ride it; again: get off. In the cart: leave the cart standing and ride on.");
            cartKey = p.Key("Keys", "CartKey", new KeyboardShortcut(KeyCode.G), "Drive the cart", "Your donkey comes with its cart and you drive; again: get off. Riding: the cart comes and is hitched.");
            p.Key("Keys", "HitchKey", new KeyboardShortcut(KeyCode.J), "Hitch / unhitch", "While riding: leave the cart standing where it is, or take the cart in reach. One key; modifiers are ignored.",
                k => Mounts.Behaviour.Set(Guid, "HitchKey", k.MainKey));
            bellKey = p.Key("Keys", "BellKey", KeyboardShortcut.Empty, "Ring the bell", "While mounted. None = no key.");
            sendAwayKey = p.Key("Keys", "SendAwayKey", KeyboardShortcut.Empty, "Send the donkey away", "Your donkey, cart and foal go (the cart's load is set down first); the next key brings a new one. None = no key.");

            p.Choice("Behaviour", "Speed", MountSpeed.Normal, "Riding speed", "How fast you go while mounted.", v => me.Speed = v, new[] { "Normal", "Faster", "Fastest" });
            p.Toggle("Behaviour", "DismountIndoors", true, "Get off at building doors", "Off: you ride into houses, and the cart goes through the furniture.", v => Mounts.Behaviour.Set(Guid, "DismountIndoors", v));
            p.Toggle("Behaviour", "ShowMessages", true, "Say why mounting is refused", "The keeper's thought bubble.", v => Mounts.Behaviour.Set(Guid, "ShowMessages", v));
            p.Toggle("Behaviour", "IdleShuffle", true, "Shuffles while standing", "Left without its cart, it takes a few slow steps now and then.", v => Mounts.Behaviour.Set(Guid, "IdleShuffle", v));
            p.Toggle("Behaviour", "KeepWithSaves", true, "Keep the donkey with the save", "After loading, your donkey and cart are where you left them, the load on the cart. They're kept in a small file next to each save (<save>.gk2donkeyapi.dat), which Steam Cloud syncs along with your saves.", v => me.KeepWithSaves = v);

            p.Preview(me);
            p.LookButton("Donkey", "Choose colours", "Closes this and opens the colours over the game, so you see your donkey and cart change. Done keeps them, Cancel puts them back.");
            p.Text("Donkey", "Name", "", "Name", "Shown when you walk up to it: \"[E] Ride Bella\". Empty: no name.", v => me.Name = v);
            ConfigEntry<CoatColor> coat = p.Choice("Donkey", "Coat", CoatColor.Game, "Coat", "", v => me.Coat = v, new[] { "The game's", "Brown", "Dark", "Cream", "Black", "Custom" }, look: true);
            p.Color("Donkey", "CustomCoat", "Custom coat", "Used when the coat is Custom.", PaletteColor.Chestnut, v => me.CustomCoat = v, () => coat.Value == CoatColor.Custom);
            ConfigEntry<HarnessColor> harness = p.Choice("Donkey", "Harness", HarnessColor.Game, "Harness", "", v => me.Harness = v, new[] { "The game's", "Black", "Red", "Blue", "Green", "Custom" }, look: true);
            p.Color("Donkey", "CustomHarness", "Custom harness", "Used when the harness is Custom.", PaletteColor.Wine, v => me.CustomHarness = v, () => harness.Value == HarnessColor.Custom);
            p.Toggle("Donkey", "Brays", true, "Brays", "Now and then, and when you get on.", v => me.Brays = v);
            p.Toggle("Donkey", "Foal", false, "A foal", "A small donkey that stays with yours and follows it around.", v => me.HasFoal = v);

            ConfigEntry<CartPaint> paint = p.Choice("Cart", "Paint", CartPaint.Game, "Paint", "", v => me.Paint = v, new[] { "The game's", "Dark wood", "Red", "Green", "Blue", "Custom" }, look: true);
            p.Color("Cart", "CustomPaint", "Custom paint", "Used when the paint is Custom.", PaletteColor.Brick, v => me.CustomPaint = v, () => paint.Value == CartPaint.Custom);
            p.Toggle("Cart", "Lantern", true, "Lantern", "The lantern on its pole.", v => me.Lantern = v);
            p.Toggle("Cart", "LanternByDay", true, "Lantern lit by day", "Off: it goes dark by day, like your hand lantern.", v => me.LanternByDay = v);
            ConfigEntry<LanternColor> light = p.Choice("Cart", "LanternColor", LanternColor.Game, "Lantern light", "", v => me.LanternLight = v, new[] { "Warm (the game's)", "White", "Green", "Blue", "Custom" }, look: true);
            p.Color("Cart", "CustomLantern", "Custom light", "Used when the lantern light is Custom.", PaletteColor.Cream, v => me.CustomLantern = v, () => light.Value == LanternColor.Custom);
            p.Toggle("Cart", "Bell", true, "Bell when setting off", "When the cart sets off with a load after standing.", v => me.BellOnSetOff = v);

            p.Steps("Sound", "Volume", 100, 0, 150, 10, "Donkey and cart sounds", "Hoofsteps, wheels, brays, the bell; on top of the game's SFX volume.", v => me.SoundVolume = v / 100f, v => v + "%");
            p.Publish();
            Log.LogInfo($"GK2 Saddle Up! {Version} loaded (GK2 Donkey API {Mounts.ApiVersion}): ride {rideKey.Value}, cart {cartKey.Value}.");
        }

        private void Update()
        {
            if (me == null || !Mounts.IsAvailable)
            {
                return;
            }
            try
            {
                if (Pressed(rideKey)) Report(me.Use(MountStyle.Ride));
                else if (Pressed(cartKey)) Report(me.Use(MountStyle.Cart));
                else if (Pressed(sendAwayKey)) me.SendAway();
                if (Pressed(bellKey) && Mounts.IsMounted) Mounts.RingBell();
            }
            catch (Exception e)
            {
                Log.LogError("Key handling failed: " + e);
            }
        }

        private static void Report(MountResult r)
        {
            if (!r.Ok)
            {
                Log.LogInfo("Not now: " + r.Reason);
            }
        }

        // The main key pressed this frame with the shortcut's own modifiers held (e.g. Shift+H); other keys held don't
        // matter. Not KeyboardShortcut.IsDown(): that also demands that no other key is held, so it never fires while
        // the player walks (W/A/S/D down), which is exactly when mounting matters (found in game 2026-09-30).
        private static bool Pressed(ConfigEntry<KeyboardShortcut> key)
        {
            KeyboardShortcut k = key.Value;
            if (k.MainKey == KeyCode.None || !UnityInput.Current.GetKeyDown(k.MainKey))
            {
                return false;
            }
            foreach (KeyCode m in k.Modifiers)
            {
                if (!UnityInput.Current.GetKey(m))
                {
                    return false;
                }
            }
            return true;
        }

        private void OnDestroy()
        {
            try
            {
                Mounts.ClearTuning(Guid);
            }
            catch
            {
            }
        }
    }
}
