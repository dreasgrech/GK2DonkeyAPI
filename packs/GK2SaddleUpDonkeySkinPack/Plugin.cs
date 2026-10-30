using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx;
using GK2DonkeyAPI.Api;

namespace GK2SaddleUpDonkeySkinPack
{
    // GK2 Saddle Up: Donkey Skins (docs/making-a-mount.md "Built in or external"): three skins for the game's donkey,
    // Hellfire, Royal Parade and Armored, embedded in this DLL as a mount folder without a mount.cfg (gk2mounts\donkey\skins\:
    // skins added to a mount registered elsewhere) and handed to the API in Awake, with the effect Hellfire is drawn for.
    // A skins-only pack: no mount of its own; the way any mod ships skins for a mount another mod owns (their names carry
    // this pack's prefix, saddleup-donkey-skins.). Saddle Up offers them in its Mounts panel.
    [BepInPlugin(Guid, "GK2 Saddle Up: Donkey Skin Pack", Version)]
    [BepInDependency("dreas.gk2.donkeyapi", "0.40.0")] // the API it was written against: an older one is refused by BepInEx
    [BepInDependency("dreas.gk2.saddleup", BepInDependency.DependencyFlags.SoftDependency)] // after Saddle Up when it's there: it offers the skins
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "dreas.gk2.saddleup.donkeyskins";
        public const string Version = "1.0.0"; // the same as CHANGELOG.md's first heading (check-dist.py checks it)
        private const string Prefix = "GK2SaddleUpDonkeySkinPack.mounts.";

        private void Awake()
        {
            int skins = Register();
            Logger.LogInfo($"GK2 Saddle Up: Donkey Skins {Version}: {skins} skin(s) registered for the donkey, and the effect saddleup-donkey-skins.hellfire.");
        }

        // In a method of its own, as the API's docs advise: a mod's Awake that touched a member the running API lacks would
        // fail before anything else in it ran. BepInEx has refused an API older than 0.40.0 already.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private int Register()
        {
            int skins = Mounts.Looks.RegisterEmbedded(Guid, Assembly.GetExecutingAssembly(), Prefix);
            RegisterHellfire();
            return skins;
        }

        // Hellfire's effect: the lamps' flame on a ring of six and their sparks on three of those spots, a flame left
        // behind. Registered and ready, shown nowhere yet: nothing picks an effect in this version (2026-10-25; how a skin
        // comes with its effect is being designed). It came with Saddle Up until 2026-10-27 as "saddleup.hellfire" (and
        // "hellfire" before 2026-10-26), never released; the API finds it by either old name.
        private void RegisterHellfire()
        {
            MountEffects api = Mounts.Effects;
            var d = new MountEffectDef { Id = "saddleup-donkey-skins.hellfire", Name = "Hellfire", Author = "dreas", Version = "1.0" };
            var flame = new MountEffectPart { Source = api.GameParticles("flame_fx", "Assets/AddressableAssets/Materials/flame_fx.mat") };
            flame.Emitters.Add(new MountEffectEmitter { Placement = MountEffectPlacement.Ring, Count = 6, Radius = 0.8f, Size = 1.5f, Height = 0.05f });
            flame.Emitters.Add(new MountEffectEmitter { Placement = MountEffectPlacement.Trail, Size = 1.2f, Height = 0.05f, Every = 0.5f, Burst = 0.4f });
            var sparks = new MountEffectPart { Source = api.GameParticles("sparks_fx", "Assets/AddressableAssets/Materials/sparks_fx.mat") };
            sparks.Emitters.Add(new MountEffectEmitter { Placement = MountEffectPlacement.Ring, Count = 3, Radius = 0.8f, Size = 1.5f, Height = 0.05f });
            d.Parts.Add(flame);
            d.Parts.Add(sparks);
            api.Register(Guid, d);
        }
    }
}
