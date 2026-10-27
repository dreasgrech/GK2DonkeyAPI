using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx;
using GK2DonkeyAPI.Api;

namespace SaddleUpCritters
{
    // GK2 Saddle Up: Critters (docs/making-a-mount.md "Built in or external"): the rat, snail and worm, mounts of their
    // own, embedded in this DLL as their pack folders (gk2mounts\saddleup-critters.rat\, .snail\, .worm\) and handed to the API in Awake. Nothing else: the API
    // draws them, seats the rider, hitches their carts; Saddle Up offers them in its Mounts panel. The way any mod ships a mount pack.
    [BepInPlugin(Guid, "GK2 Saddle Up: Critters", Version)]
    [BepInDependency("dreas.gk2.donkeyapi", "0.40.0")] // the API it was written against: an older one is refused by BepInEx
    [BepInDependency("dreas.gk2.saddleup", BepInDependency.DependencyFlags.SoftDependency)] // after Saddle Up when it's there: it offers the mounts to ride
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "dreas.gk2.saddleup.critters";
        public const string Version = "1.0.0"; // the same as CHANGELOG.md's first heading (check-dist.py checks it)
        private const string Prefix = "SaddleUpCritters.mounts.";

        private void Awake()
        {
            int skins = Register();
            Logger.LogInfo($"GK2 Saddle Up: Critters {Version}: {skins} skin(s) registered (the mounts saddleup-critters.rat, .snail, .worm).");
        }

        // In a method of its own, as the API's docs advise: a mod's Awake that touched a member the running API lacks would
        // fail before anything else in it ran. BepInEx has refused an API older than 0.40.0 already.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private int Register() => Mounts.Looks.RegisterEmbedded(Guid, Assembly.GetExecutingAssembly(), Prefix);
    }
}
