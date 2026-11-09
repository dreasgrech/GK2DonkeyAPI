using System.Collections.Generic;
using static GK2DonkeyAPI.SelfTest;

namespace GK2DonkeyAPI.IntegrationTests
{
    // The integration scenarios: each joins the API's runner in its group (SelfTest.AddScenarios), after the API's own
    // scenarios of that group, before the run's own checks (docs/self-tests.md).
    internal static partial class Tests
    {
        internal static readonly List<Scenario> Scenarios = new List<Scenario>
        {
            // the scenarios, in the API's groups
            new Scenario { Group = GroupCore, Name = "upgrade: Saddle Up's cfg as every released version wrote it lands on today's page", Body = UpgradeSaddleUpCfg },
            new Scenario { Group = GroupCargo, Name = "cargo: each mount carries its own (a lighter mount leaves an over-full cart standing)", Body = CargoPerMount },
            new Scenario { Group = GroupLooks, Name = "speed: our mounts' words and cargo as their packs say them; Saddle Up's speed rows retired", Body = ShippedSpeeds },
            new Scenario { Group = GroupLooks, Name = "skin: on, off, removed (pictures loaded and freed)", Body = SkinOnOff },
            new Scenario { Group = GroupLooks, Name = "skins: no leaks over spawn and remove", Body = SkinLeaks },
            new Scenario { Group = GroupLooks, Name = "skin: kept through a hitch, freed asleep far away, worn again awake", Body = SkinSleep },
            new Scenario { Group = GroupLooks, Name = "effect: hellfire lit (the test hook), follows, out, gone with the donkey", Body = EffectOnOff },
            new Scenario { Group = GroupLooks, Name = "built in or external: our mounts and the donkey's skins from their packs' own DLLs (owner, plugin, needs, Hellfire's effect), Larry from Saddle Up's, the skins offered though late, no old ids", Body = ExternalPacks },
            new Scenario { Group = GroupLooks, Name = "game creature: Larry's frames found, scaled and worn", Body = GameCreature },
            new Scenario { Group = GroupLooks, Name = "seat: the rider's height on every shipped mount, AutoSeat off and on (a readout)", Body = SeatReadout },
            new Scenario { Group = GroupLooks, Name = "cart: the hitch gap and the cart-style seat on every shipped mount (a readout)", Body = CartReadout },
            new Scenario { Group = GroupLooks, Name = "effect: the ring's size by the worn skin's drawn width (the test hook)", Body = SkinEffect },
            new Scenario { Group = GroupLooks, Name = "front layer: the dragon's near wing stays in front of the rider (a readback)", Body = FrontLayer },
            new Scenario { Group = GroupLooks, Name = "preview: the cart sits at the new mount's hitch gap as soon as the mount is switched", Body = PreviewGap },
            new Scenario { Group = GroupLooks, Name = "skins: Saddle Up's dragon with a skin not installed: the row keeps the choice, its colour rows are the dragon's own look's", Body = SaddleUpMissingSkinRows },
            new Scenario { Group = GroupLooks, Name = "skins: a slow stand-in (Larry with a skin not installed) is worn though its rig is refreshed every frame while it builds", Body = MissingSkinSlowBuild },
            new Scenario { Group = GroupLooks, Name = "colours: the Coat row reaches every body colour of every shipped mount, and no other", Body = CoatEveryBuiltIn },
            new Scenario { Group = GroupLooks, Name = "colours: the Second tone row reaches the rat's pink parts, the snail's shell and the dragon's belly, and is left out elsewhere", Body = SecondTone },
            new Scenario { Group = GroupLooks, Name = "colours: the outline is a part of its own: its Outline row colours it, Legs and mane and the Coat don't (the donkey, Hellfire; the dragon's outline no row)", Body = OutlineAsDrawn },
            new Scenario { Group = GroupLooks, Name = "colours: the donkey's Muzzle row reaches its nose only; the face's dots are its Coat, the belly's line its outline", Body = MuzzleNoseOnly },
            new Scenario { Group = GroupLooks, Name = "colours: the donkey's eye and nostril repainted: the eye's line as drawn under Legs and mane, the nostril follows the Muzzle (the plain donkey and Hellfire)", Body = FacePixelsPairs },
            new Scenario { Group = GroupLooks, Name = "colours: a colour picked for a part a skin pre-colours replaces the pre-colour (Hellfire's ash coat), a shade stays on it", Body = PickedColourOverBase },
            new Scenario { Group = GroupLooks, Name = "colours: Hellfire gives the donkey's eye no row (its glowing eyes cover it), its other rows kept", Body = HellfireNoEyesRow },
            new Scenario { Group = GroupLooks, Name = "over skins: Hellfire and Royal Parade worn, recoloured and walking; the snail's own rider offset (a readback)", Body = OverBuiltIns },
            new Scenario { Group = GroupLooks, Name = "colours: the player's regions on a mount in its own colours", Body = SkinColours },
            new Scenario { Group = GroupFlying, Name = "keys: Saddle Up's Jump and Fly rows (Space, shown only while flying is on, the Behaviour keys follow)", Body = FlightKeyRows },
            new Scenario { Group = GroupSounds, Name = "the dragon's sound files: the flight's in its pack's mount folder (the mount's, for every dragon skin), none the red dragon's own, decoded once through the cache from the pack's DLL", Body = SoundsDragonFiles },
            new Scenario { Group = GroupLevels, Name = "babies: Saddle Up's babies rows, in the journal, reaching its companion", Body = SaddleUpBabiesRows },
            new Scenario { Group = GroupLevels, Name = "babies: Saddle Up's own babies, each its own temperament", Body = SaddleUpFoalTempers },
            new Scenario { Group = GroupLevels, Name = "mounts left in the world: Saddle Up's row (the options window's Mount section, off by default, reaching its companion)", Body = SaddleUpLeaveMountsRow },
            new Scenario { Group = GroupIndoors, Name = "indoors: Saddle Up's two rows (the second greys while the first is off; the first reaches the API)", Body = SaddleUpIndoorsRows },
            new Scenario { Group = GroupLooks, Name = "layering: the Donkey Skins pack's two skins have no default, nothing resolved, as before", Body = DonkeySkinsUnlayered },
            new Scenario { Group = GroupLooks, Name = "freeplay: Saddle Up's Freeplay rows (the last section, the API's words, shown with the switch on, each driving its key)", Body = SaddleUpFreeplayRows },
            new Scenario { Group = GroupLooks, Name = "options: Saddle Up's five tabs (Mount Behaviour and Sound under their headings in Mount, which fits without a scrollbar; the journal button named as the window; Send your mount away after the keys that call it)", Body = SaddleUpTabs },
            new Scenario { Group = GroupLooks, Name = "settings: with the Mounts panel open Saddle Up's ride and cart keys work only as its Ride and Drive buttons, its panel key works", Body = SaddleUpKeysOverPanel },
            new Scenario { Group = GroupSafety, Name = "safe spots: Saddle Up's rows (the toggle drives FallGuard, the button brings him back, the key reaches ReturnKey)", Body = SaddleUpSafeSpotRows },
            new Scenario { Group = GroupSounds, Name = "a standing snail: no voice and no lips by its [Sounds] and the rule, its step its own squelch", Body = SnailStandingSounds },
            new Scenario { Group = GroupSounds, Name = "Saddle Up's \"Standing mounts and babies sound\" row drives the Behaviour key", Body = SaddleUpStandingSoundsRow },
            new Scenario { Group = GroupFlying, Name = "mounts: our mounts' flies and jumps flags (the dragon flies, the rest jump and don't)", Body = ShippedFlightFlags },
            new Scenario { Group = GroupFlying, Name = "jump: the dragon's own (its [Jump] with wings; live, a take-off and landing on its flying frames)", Body = DragonJump },
            new Scenario { Group = GroupFlying, Name = "rider: still on the dragon's flying pictures while the game walks under them (the stick held in the air), its own BobInFlight bringing the motion back, the bob on the ground after", Body = DragonRiderSteady },
            new Scenario { Group = GroupGarbage, Name = "the garbage survey (measures only): on foot standing and walking, riding walking and still, the Mounts panel picking, the dev stride probe; the garbage by part in each", Body = GarbageSurvey, HidesPanel = true },
            new Scenario { Group = GroupGarbage, Name = "the mount crowd (measures only): every shipped look with babies and carts, lanterns lit at night, uncapped; 12 then 36 in view, 120 more scattered (the levels), ridden through; rows in BepInEx\\GK2-crowd.tsv; then everything gone and the counters back", Body = CrowdTests.MountCrowd, HidesPanel = true },
            new Scenario { Group = GroupWalk, Name = "the walk survey (measures only): every mount ridden and with the cart, steered right and left by the test, a standing one and a baby near; the stride probe's rows in BepInEx/GK2-strides", Body = WalkSurvey },
        };
    }
}
