using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using GK2DonkeyAPI.Api;
using HarmonyLib;
using UnityEngine;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("PackerScan")] // tests/PackerScan runs the cargo packer without the game

namespace GK2DonkeyAPI
{
    // Everything that positions and animates rider and donkey, one complete set per style ([Ride] and [Cart]
    // sections in the cfg). The two looks differ in almost every value, so they are tuned independently; the
    // tuning panel always shows the set of the style currently mounted.
    internal sealed class StyleSettings
    {
        public readonly string Section;
        public readonly Tuned<float> SpeedMultiplier;
        public readonly Tuned<float> WalkCycleSpeed;   // the walk clip's rate per unit of walking speed: the stride (the donkey's 1.5)
        public readonly Tuned<float> FocusSpeed;
        public readonly Tuned<float> Depth;
        public readonly Tuned<float> CenterOffset;
        public readonly Tuned<int> RiderOffsetPx;
        public readonly Tuned<int> TorsoOffsetPx;
        public readonly Tuned<bool> AutoSeat;
        public readonly Tuned<int> SeatAdjustPx;
        public readonly Tuned<float> SeatOffset;
        public readonly Tuned<bool> Bob;
        public readonly Tuned<RiderPose> Pose;
        public readonly Tuned<bool> FreezePoseWhenIdle;
        public readonly Tuned<float> PoseSpeed;
        public readonly Tuned<bool> PoseSpeedFollowsDonkey;
        public readonly Tuned<bool> NearLeg;
        public readonly Tuned<LegSource> LegSource;
        public readonly Tuned<int> LegHeightPx;
        public readonly Tuned<int> LegAdjustXPx;
        public readonly Tuned<int> LegAdjustYPx;
        public readonly Tuned<int> BackSeatExtraPx;
        public readonly Tuned<bool> SnapToGround;
        public readonly Tuned<int> HitchGapPx;
        public readonly Tuned<bool> CartFollowsGround;
        public readonly Tuned<float> CartGroundMaxShift;
        public readonly Tuned<CartSlopeStyle> CartSlope;
        public readonly Tuned<float> CartPitchMaxDegrees;
        public readonly Tuned<bool> CargoDrawCorpse, CargoSwaysWithCart, CargoSetDownBehindCart;
        public readonly Tuned<float> CargoBedDepth, CargoSideGap, CargoOverhang, CargoNestle, CargoLayerShift, CargoSetDownGap;
        public readonly Tuned<float> LanternAlong, LanternUp, LanternAcross, LanternTilt, LanternLean;
        public readonly Tuned<bool> LanternFlip;
        public readonly Tuned<bool> LanternHangs;
        public readonly Tuned<float> LanternSwayDegrees, LanternSwaySeconds;
        public readonly Tuned<bool> LanternShow, LanternLightOn, LanternLightShadows, LanternSelfLightOn, LanternGlowOn, LanternGlassOn;
        public readonly Tuned<float> LanternScale, LanternLightIntensity, LanternLightRange, LanternLightDayDim, LanternSelfLightIntensity, LanternSelfLightRange, LanternGlowAlpha, LanternGlowScale;
        public readonly Tuned<string> LanternLightColor;
        public readonly Tuned<float> IdleFidgetEverySeconds, IdleFidgetSpeed, IdleCrawlSpeed;
        // Rider motion while the donkey walks (docs/rider-motion.md): the body's bob, the hands and the head.
        public readonly Tuned<float> BobScale, HandsSteady, HandsSwayPx, HeadSettle;
        public readonly Tuned<int> BobLagFrames, HeadLagFrames;
        // Sounds tab (Sounds.cs: the base layer of every mount's sounds). Volumes multiply the game's own volume for that sound (1 = as the game plays it).
        public readonly Tuned<bool> SoundsOn, HoofOn, HoofSurfaceAware, GrazeOn, BrayWhileWalking, BrayInSfxChannel,
            CargoThumpOn, WheelOn, WheelPitchFollowsSpeed, BellOnSetOff, BellOnlyWithCargo, MountOn, DismountOn, HitchOn, UnhitchOn;
        public readonly Tuned<float> SoundVolume, HoofVolume, HoofPitch, HoofPitchVariation, HoofPhase, GrazeVolume, LipsVolume,
            BrayOnMountChance, BrayIdleEverySeconds, BrayVolume, BrayPitch, CargoThumpVolume, CargoThumpPitch,
            WheelVolume, WheelPitch, WheelPhase, BellVolume, BellPitch, BellSetOffIdleSeconds,
            MountVolume, MountPitch, MountSoundStart, DismountVolume, DismountPitch, DismountSoundStart, HitchVolume, HitchPitch, UnhitchVolume, UnhitchPitch;
        public readonly Tuned<int> HoofStepsPerCycle, WheelEveryCycles;
        public readonly Tuned<string> HoofSoundId, GrazeSoundId, BraySoundId, CargoThumpSoundId, WheelSoundId, BellSoundId,
            MountSoundId, DismountSoundId, HitchSoundId, UnhitchSoundId;

        public StyleSettings(ConfigFile cfg, string section, float speed, float depth, float center, int riderPx, int torsoPx,
            bool autoSeat, int seatAdjustPx, float seatOffset, bool bob, RiderPose pose, bool freeze,
            bool legs, LegSource legSource, int legHeight, int legX, int legY, float poseSpeed = 1f, bool poseSpeedFollows = true)
        {
            Section = section;
            string who = section == "Cart" ? "in cart style" : "while riding";
            SpeedMultiplier = cfg.Bind(section, "SpeedMultiplier", Defaults.Get(section, "SpeedMultiplier", speed),
                new ConfigDescription($"Walking speed {who}, as a multiple of normal (measured: exactly linear).", new AcceptableValueRange<float>(0.5f, 4f)));
            WalkCycleSpeed = cfg.Bind(section, "WalkCycleSpeed", Defaults.Get(section, "WalkCycleSpeed", 1.5f),
                new ConfigDescription($"How fast the walk animation plays {who} for each unit of walking speed: the stride. 1.5 is the donkey's (the game's own rate, a constant until 0.35.0); a mount with a longer stride sets less in its [Mount.Tuning], a scurrying one more, so its legs match its speed (docs/mounts-foundation-2.md 2.4).", new AcceptableValueRange<float>(0.5f, 4f)));
            FocusSpeed = cfg.Bind(section, "FocusSpeed", Defaults.Get(section, "FocusSpeed", 0.5f),
                new ConfigDescription($"Speed {who} in a fight's focus stance (right click held: aiming while moving), as a fraction of the speed above. The game halves the keeper's own walking speed there; 1 keeps the full speed.", new AcceptableValueRange<float>(0.25f, 1f)));
            Depth = cfg.Bind(section, "Depth", Defaults.Get(section, "Depth", depth),
                "Donkey offset towards the camera (negative, drawn in front of the keeper) or away from it (positive, behind), in world units.");
            CenterOffset = cfg.Bind(section, "CenterOffset", Defaults.Get(section, "CenterOffset", center),
                "Above 0: keep the donkey centred under the rider (its sprite's built-in sideways offset is measured and cancelled every frame). 0: leave the donkey where the game draws it, half a body to the side.");
            RiderOffsetPx = cfg.Bind(section, "RiderOffsetPx", Defaults.Get(section, "RiderOffsetPx", riderPx),
                "Where the keeper stands or sits along the donkey, in art pixels: positive = closer to the head, negative = closer to the rear.");
            TorsoOffsetPx = cfg.Bind(section, "TorsoOffsetPx", Defaults.Get(section, "TorsoOffsetPx", torsoPx),
                "Shifts only the keeper's upper body along the donkey, in art pixels (positive = towards the head); the legs stay where they are.");
            AutoSeat = cfg.Bind(section, "AutoSeat", Defaults.Get(section, "AutoSeat", autoSeat),
                "Compute the torso height from the sprites (rider's hip on the donkey's back line). Off: use SeatOffset.");
            SeatAdjustPx = cfg.Bind(section, "SeatAdjustPx", Defaults.Get(section, "SeatAdjustPx", seatAdjustPx),
                "With AutoSeat: fine adjustment of the automatic height, in art pixels (positive = higher).");
            SeatOffset = cfg.Bind(section, "SeatOffset", Defaults.Get(section, "SeatOffset", seatOffset),
                "Without AutoSeat: torso height in world units (0 = standing on the ground).");
            Bob = cfg.Bind(section, "Bob", Defaults.Get(section, "Bob", bob),
                "Move the rider up and down with the donkey's back through its walk cycle (the back moves 1 px either way; BobScale multiplies it).");
            Pose = cfg.Bind(section, "Pose", Defaults.Get(section, "Pose", pose),
                "Keeper's body pose. FishingIdle: hands forward as if holding reins, legs still (left/right frames only). Idle: standing still. FocusedWalk: the combat-focus walk. Walk: unchanged (arms swing).");
            FreezePoseWhenIdle = cfg.Bind(section, "FreezePoseWhenIdle", Defaults.Get(section, "FreezePoseWhenIdle", freeze),
                "Pause the keeper's animation while standing still, so the arms stop moving; resumes when moving. Not used for the Idle and Walk poses.");
            PoseSpeed = cfg.Bind(section, "PoseSpeed", Defaults.Get(section, "PoseSpeed", poseSpeed),
                new ConfigDescription("Playback speed of the keeper's pose animation while the donkey moves (1 = the clip's own speed).", new AcceptableValueRange<float>(0.1f, 4f)));
            PoseSpeedFollowsDonkey = cfg.Bind(section, "PoseSpeedFollowsDonkey", Defaults.Get(section, "PoseSpeedFollowsDonkey", poseSpeedFollows),
                "Also multiply the pose animation speed by the riding speed multiplier, so it keeps pace with the donkey's walk animation.");
            NearLeg = cfg.Bind(section, "NearLeg", Defaults.Get(section, "NearLeg", legs),
                "Draw the rider's legs in front of the donkey while his real (baked-in) legs are hidden inside it.");
            LegSource = cfg.Bind(section, "LegSource", Defaults.Get(section, "LegSource", legSource),
                "Body: the keeper's own trousers and boots, re-sliced from his current body frame (outfit-exact, follows the pose). Drawn: a hand-drawn bent leg in outfit colours.");
            LegHeightPx = cfg.Bind(section, "LegHeightPx", Defaults.Get(section, "LegHeightPx", legHeight),
                "With LegSource=Body: how many bottom rows of the body frame count as legs (feet to belt), in art pixels.");
            LegAdjustXPx = cfg.Bind(section, "LegAdjustXPx", Defaults.Get(section, "LegAdjustXPx", legX),
                "Fine adjustment of the legs along the donkey, relative to the torso, in art pixels (positive = towards the rear).");
            LegAdjustYPx = cfg.Bind(section, "LegAdjustYPx", Defaults.Get(section, "LegAdjustYPx", legY),
                "Fine adjustment of the legs' height, relative to the donkey's back line, in art pixels (positive = higher).");
            BackSeatExtraPx = cfg.Bind(section, "BackSeatExtraPx", Defaults.Get(section, "BackSeatExtraPx", 5),
                "Seen from behind in armour (aiming up) the rider sits side-saddle, legs hidden on the far side: how much higher he sits then, in art pixels, so the armour skirt's hem meets the donkey's back.");
            SnapToGround = cfg.Bind(section, "SnapToGround", Defaults.Get(section, "SnapToGround", true),
                "Place the donkey at the ground height under its own position (the game does this for every walking object). Off: it takes the keeper's height, which digs it into slopes when it stands beside or behind him.");
            HitchGapPx = cfg.Bind(section, "HitchGapPx", Defaults.Get(section, "HitchGapPx", 0),
                new ConfigDescription("Cart style only: the whole cart (shafts, wheels, body, lantern, load) sits this much further behind the mount, in art pixels (negative: closer). 0 for the donkey; a longer mount sets its own through its mount file, so its body isn't drawn inside the cart.", new AcceptableValueRange<int>(-32, 64)));
            CartFollowsGround = cfg.Bind(section, "CartFollowsGround", Defaults.Get(section, "CartFollowsGround", true),
                "Cart style only: raise or lower the whole cart (wheels, cart body, lantern) as one, every frame, by the ground difference between the cart and the donkey, so the rigid 3D cart does not sink into a slope behind the donkey. The bars between donkey and cart take half of that shift. Nothing rotates.");
            CartGroundMaxShift = cfg.Bind(section, "CartGroundMaxShift", Defaults.Get(section, "CartGroundMaxShift", 0.8f),
                "Largest shift the cart may get from CartFollowsGround, in world units, either way.");
            CartSlope = cfg.Bind(section, "CartSlope", Defaults.Get(section, "CartSlope", CartSlopeStyle.WholeCartPitch),
                "Cart style, with CartFollowsGround: how the cart meets a slope. WholeCartPitch: the wheels follow the ground and the cart (body, shafts, pole, cargo) pitches about the axle so the shafts stay at the donkey, as a real cart does. ShaftsTilt: the cart is lifted level and only the shafts turn about their harness end. HalfLift: the cart is lifted level and the shafts by half (a gap at both ends).");
            CartPitchMaxDegrees = cfg.Bind(section, "CartPitchMaxDegrees", Defaults.Get(section, "CartPitchMaxDegrees", 20f),
                new ConfigDescription("WholeCartPitch: the steepest the cart may pitch, in degrees. Steeper ground leaves the shafts short of the donkey instead of tipping the pole and cargo further.", new AcceptableValueRange<float>(0f, 45f)));
            CargoDrawCorpse = cfg.Bind(section, "CargoDrawCorpse", Defaults.Get(section, "CargoDrawCorpse", true),
                "Cart style only: the mod draws every corpse on the cart itself (same packer and corrections as any other item) and keeps the game's loaded-cart body off. Off: the first corpse is shown by the game's own loaded cart body with the shroud, which the packer cannot move; other items then ride on top of it.");
            CargoSwaysWithCart = cfg.Bind(section, "CargoSwaysWithCart", Defaults.Get(section, "CargoSwaysWithCart", true),
                "Cart style only: the cargo rocks with the cart while it rolls, as the game's own loaded corpse does (the walk animation rotates the cart body about the axle by up to 3 degrees; the cargo holder copies that rotation every frame). Off: the cargo stays level.");
            CargoSetDownBehindCart = cfg.Bind(section, "CargoSetDownBehindCart", Defaults.Get(section, "CargoSetDownBehindCart", true),
                "Cart style only: the interact key sets the top cart item down behind the cart (the game's own drop-spot search, run from the cart's back end outwards), out of the keeper's reach, so pressing twice sets both items down. Off: in front of the keeper, as the game drops a held item, where the next press picks it up again. When there is no free spot behind the cart the item goes in front.");
            CargoSetDownGap = cfg.Bind(section, "CargoSetDownGap", Defaults.Get(section, "CargoSetDownGap", 0.1f),
                new ConfigDescription("Cart style, with CargoSetDownBehindCart: the gap between the back end of the cart body and the near end of an item set down behind it, world units. The item's own measured length decides where its centre goes, so every size keeps this gap. A taken spot moves the next item 0.1 further out.", new AcceptableValueRange<float>(0f, 1.5f)));
            CargoBedDepth = cfg.Bind(section, "CargoBedDepth", Defaults.Get(section, "CargoBedDepth", -0.26f),
                "Cart style only: the cargo floor relative to the measured top of the empty cart body, world units (negative = items sink below the rails onto the bed floor). Every item's measured bottom is put on this floor.");
            CargoSideGap = cfg.Bind(section, "CargoSideGap", Defaults.Get(section, "CargoSideGap", 0.02f),
                "Cart style only: gap between two items lying next to each other (across the bed or end to end along it), world units.");
            CargoOverhang = cfg.Bind(section, "CargoOverhang", Defaults.Get(section, "CargoOverhang", 0.12f),
                "Cart style only: how far two items lying next to each other may together exceed the bed's measured width (across) or length (end to end) before the second one is stacked on the first instead, world units.");
            CargoNestle = cfg.Bind(section, "CargoNestle", Defaults.Get(section, "CargoNestle", 0.03f),
                "Cart style only: how far an item stacked on another sinks into it, world units (a log on a shroud does not float on the highest point).");
            CargoLayerShift = cfg.Bind(section, "CargoLayerShift", Defaults.Get(section, "CargoLayerShift", -0.03f),
                new ConfigDescription("Cart style only, with more than two items (CartSlots): how far each layer above the bed is moved along the cart, per layer, world units; positive = towards the back of the cart, negative = towards the donkey. The default leans the load a little towards the donkey, away from the lantern pole, which stands at the back in Cart style.", new AcceptableValueRange<float>(-0.3f, 0.3f)));
            LanternAlong = cfg.Bind(section, "LanternAlong", Defaults.Get(section, "LanternAlong", 0f),
                "Cart style only: move the lantern pole (with its light) along the cart, world units; positive = towards the back of the cart, away from the donkey. The prefab has it at the front rail, where the rider's head hides it.");
            LanternUp = cfg.Bind(section, "LanternUp", Defaults.Get(section, "LanternUp", 0f),
                "Cart style only: move the lantern pole up (positive) or down, world units.");
            LanternAcross = cfg.Bind(section, "LanternAcross", Defaults.Get(section, "LanternAcross", 0f),
                "Cart style only: move the lantern pole across the cart, world units; positive = towards the camera (in front of the rider), negative = away (behind him).");
            LanternTilt = cfg.Bind(section, "LanternTilt", Defaults.Get(section, "LanternTilt", 0f),
                new ConfigDescription("Cart style only: tilt the pole about its foot in the screen plane, degrees; positive = its top leans towards the back of the cart. The lamp and its light follow the top; the cart's walking sway is kept.", new AcceptableValueRange<float>(-60f, 60f)));
            LanternLean = cfg.Bind(section, "LanternLean", Defaults.Get(section, "LanternLean", 0f),
                new ConfigDescription("Cart style only: lean the pole about its foot towards the camera (positive) or away from it, degrees. The lamp follows the top; its sprites stay upright.", new AcceptableValueRange<float>(-60f, 60f)));
            LanternHangs = cfg.Bind(section, "LanternHangs", Defaults.Get(section, "LanternHangs", true),
                "Cart style only: the iron lamp hangs straight down from the pole's hook (with its lit glass, glow and lights) while the pole tips with a slope, rocks while walking, or is tilted or leaned on the Lantern tab. Off: the lamp turns with the pole, as in the game.");
            LanternFlip = cfg.Bind(section, "LanternFlip", Defaults.Get(section, "LanternFlip", false),
                "Cart style only: mirror the pole in the plane through its foot, so the slanted pole leans out over the back of the cart instead of forward over the donkey; the foot stays where it is. The lamp, its glow and its lights are mirrored with it.");
            LanternSwayDegrees = cfg.Bind(section, "LanternSwayDegrees", Defaults.Get(section, "LanternSwayDegrees", 1.5f),
                new ConfigDescription("Cart style only: a slow pendulum sway of the lantern pole about its foot, in degrees either way (0 = off). Scaled by the game's wind: about half this in still air, up to one and a half times in a gale. The lamp and its light follow, so the light pool shifts with it. Adds to the cart's own rocking while it rolls.", new AcceptableValueRange<float>(0f, 10f)));
            LanternSwaySeconds = cfg.Bind(section, "LanternSwaySeconds", Defaults.Get(section, "LanternSwaySeconds", 2.6f),
                new ConfigDescription("Cart style only: period of one full sway, seconds. The amplitude drifts slowly so it is not a metronome.", new AcceptableValueRange<float>(0.5f, 10f)));
            LanternShow = cfg.Bind(section, "LanternShow", Defaults.Get(section, "LanternShow", true),
                "Cart style only: show the lantern pole and its lamp at all. Off hides pole, lamp, glow and lights.");
            LanternScale = cfg.Bind(section, "LanternScale", Defaults.Get(section, "LanternScale", 1f),
                new ConfigDescription("Cart style only: size of the pole, scaled about its foot (1 = the prefab's). The lamp moves to the scaled top; its pixel art is not scaled.", new AcceptableValueRange<float>(0.5f, 1.6f)));
            LanternLightOn = cfg.Bind(section, "LanternLightOn", Defaults.Get(section, "LanternLightOn", true),
                "Cart style only: the lamp's main point light (the game's: range 4.5, intensity 3, orange, soft shadows, lit day and night).");
            LanternLightIntensity = cfg.Bind(section, "LanternLightIntensity", Defaults.Get(section, "LanternLightIntensity", 1f),
                new ConfigDescription("Cart style only: multiplier on the main light's intensity (1 = the game's 3).", new AcceptableValueRange<float>(0f, 4f)));
            LanternLightRange = cfg.Bind(section, "LanternLightRange", Defaults.Get(section, "LanternLightRange", 1f),
                new ConfigDescription("Cart style only: multiplier on the main light's range (1 = the game's 4.5 world units).", new AcceptableValueRange<float>(0.2f, 3f)));
            LanternLightColor = cfg.Bind(section, "LanternLightColor", Defaults.Get(section, "LanternLightColor", "FF9600"),
                "Cart style only: colour of both lights as hex RRGGBB (the game's is FF9600, orange). Empty = the game's colour.");
            LanternLightShadows = cfg.Bind(section, "LanternLightShadows", Defaults.Get(section, "LanternLightShadows", true),
                "Cart style only: the main light casts soft shadows (as the game's does). Off saves a little rendering.");
            LanternLightDayDim = cfg.Bind(section, "LanternLightDayDim", Defaults.Get(section, "LanternLightDayDim", 1f),
                new ConfigDescription("Cart style only: how bright the lights are in full daylight, as a fraction of their night value (1 = always full, as the game's cart; 0 = off by day, like the keeper's own lantern). Follows the game's sun value through dusk and dawn.", new AcceptableValueRange<float>(0f, 1f)));
            LanternSelfLightOn = cfg.Bind(section, "LanternSelfLightOn", Defaults.Get(section, "LanternSelfLightOn", true),
                "Cart style only: the lamp's second light, which lights only characters and sprites nearby (the game's: range 2, intensity 10).");
            LanternSelfLightIntensity = cfg.Bind(section, "LanternSelfLightIntensity", Defaults.Get(section, "LanternSelfLightIntensity", 1f),
                new ConfigDescription("Cart style only: multiplier on the character light's intensity (1 = the game's 10).", new AcceptableValueRange<float>(0f, 4f)));
            LanternSelfLightRange = cfg.Bind(section, "LanternSelfLightRange", Defaults.Get(section, "LanternSelfLightRange", 1f),
                new ConfigDescription("Cart style only: multiplier on the character light's range (1 = the game's 2 world units).", new AcceptableValueRange<float>(0.2f, 3f)));
            LanternGlowOn = cfg.Bind(section, "LanternGlowOn", Defaults.Get(section, "LanternGlowOn", true),
                "Cart style only: the soft additive glow sprite around the lamp.");
            LanternGlowAlpha = cfg.Bind(section, "LanternGlowAlpha", Defaults.Get(section, "LanternGlowAlpha", 1f),
                new ConfigDescription("Cart style only: multiplier on the glow's opacity (1 = the game's).", new AcceptableValueRange<float>(0f, 4f)));
            LanternGlowScale = cfg.Bind(section, "LanternGlowScale", Defaults.Get(section, "LanternGlowScale", 1f),
                new ConfigDescription("Cart style only: multiplier on the glow's size (1 = the game's).", new AcceptableValueRange<float>(0.3f, 3f)));
            LanternGlassOn = cfg.Bind(section, "LanternGlassOn", Defaults.Get(section, "LanternGlassOn", true),
                "Cart style only: the lit lantern-glass sprite on the lamp (unlit shader, always bright).");
            IdleFidgetEverySeconds = cfg.Bind(section, "IdleFidgetEverySeconds", Defaults.Get(section, "IdleFidgetEverySeconds", 7f),
                new ConfigDescription("With FreezePoseWhenIdle: instead of a dead stop, the frozen pose plays for about a second every this many seconds (randomised by half either way), so the rider shifts now and then. 0 = a hard freeze as before.", new AcceptableValueRange<float>(0f, 60f)));
            IdleFidgetSpeed = cfg.Bind(section, "IdleFidgetSpeed", Defaults.Get(section, "IdleFidgetSpeed", 0.45f),
                new ConfigDescription("Playback speed of the pose during a fidget (1 = the clip's own speed).", new AcceptableValueRange<float>(0.05f, 2f)));
            IdleCrawlSpeed = cfg.Bind(section, "IdleCrawlSpeed", Defaults.Get(section, "IdleCrawlSpeed", 0.05f),
                new ConfigDescription("With FreezePoseWhenIdle: playback speed between fidgets, so the keeper's breathing layer keeps a barely visible life instead of stopping. 0 = fully still between fidgets.", new AcceptableValueRange<float>(0f, 0.5f)));

            bool riding = section != "Cart";
            BobScale = cfg.Bind(section, "BobScale", Defaults.Get(section, "BobScale", riding ? 1.5f : 1f),
                new ConfigDescription("With Bob: how far the rider bobs, as a multiple of the donkey's back (which moves 1 art px either way). 1 = exactly with the back.", new AcceptableValueRange<float>(0f, 3f)));
            BobLagFrames = cfg.Bind(section, "BobLagFrames", Defaults.Get(section, "BobLagFrames", riding ? 1 : 0),
                new ConfigDescription("With Bob: how many of the donkey's walk frames the rider's bob trails its back, so his weight follows the step. 0 = at the same time.", new AcceptableValueRange<int>(0, 3)));
            HandsSteady = cfg.Bind(section, "HandsSteady", Defaults.Get(section, "HandsSteady", riding ? 0.5f : 0f),
                new ConfigDescription("While walking: how much of the body's bob the hands (the keeper's arm layer) don't follow, as if holding the reins steady. 0 = the hands move with the body, 1 = they stay level.", new AcceptableValueRange<float>(0f, 1f)));
            HandsSwayPx = cfg.Bind(section, "HandsSwayPx", Defaults.Get(section, "HandsSwayPx", riding ? 0.5f : 0f),
                new ConfigDescription("While walking: the hands move forward and back with each of the donkey's strides (following its head), by up to this many art pixels. 0 = off.", new AcceptableValueRange<float>(0f, 2f)));
            HeadLagFrames = cfg.Bind(section, "HeadLagFrames", Defaults.Get(section, "HeadLagFrames", riding ? 1 : 0),
                new ConfigDescription("With Bob: the head (with hair, beard and eyes) settles this many walk frames after the body. 0 = the head moves with the body.", new AcceptableValueRange<int>(0, 2)));
            HeadSettle = cfg.Bind(section, "HeadSettle", Defaults.Get(section, "HeadSettle", riding ? 0.3f : 0f),
                new ConfigDescription("With HeadLagFrames: how much of that late movement the head shows. 1 = all of it (the head then moves against the body by up to two bobs at each step: a bobble head); 0 = the head moves with the body.", new AcceptableValueRange<float>(0f, 1f)));

            bool cart = section == "Cart";
            SoundsOn = B(cfg, section, "SoundsOn", true, "Master switch for all mount sounds added by the mod " + who + ".");
            SoundVolume = F(cfg, section, "SoundVolume", 1f, 0f, 3f, "Master volume multiplier for all mount sounds " + who + ".");
            HoofOn = B(cfg, section, "HoofOn", true, "Hoofsteps: the game's donkey walks silently; this plays a step sound on each footfall of its walk cycle.");
            HoofSoundId = S(cfg, section, "HoofSoundId", "", "Sound id for the hoofsteps. Empty = by the ground under the donkey (step_grass/step_dirt/step_wood/step_stone, as the game's footsteps).");
            HoofSurfaceAware = B(cfg, section, "HoofSurfaceAware", true, "With an empty HoofSoundId: pick the step sound by the surface under the donkey. Off: always step_dirt.");
            HoofVolume = F(cfg, section, "HoofVolume", 1.3f, 0f, 3f, "Hoofstep volume (x the game's footstep volume).");
            HoofPitch = F(cfg, section, "HoofPitch", 0.8f, 0.3f, 2f, "Hoofstep pitch (below 1 = heavier than the keeper's steps).");
            HoofPitchVariation = F(cfg, section, "HoofPitchVariation", 0.06f, 0f, 0.5f, "Random pitch change per hoofstep, either way, so steps do not sound identical.");
            HoofStepsPerCycle = I(cfg, section, "HoofStepsPerCycle", 4, 1, 8, "Footfalls per walk cycle of the donkey's animation (a donkey has four hooves).");
            HoofPhase = F(cfg, section, "HoofPhase", 0f, 0f, 1f, "Shifts the footfalls within the walk cycle (fraction of one step), to line them up with the art.");
            GrazeOn = B(cfg, section, "GrazeOn", true, "Play a chewing sound when the donkey grazes while standing (the game has the sound, made for the graze animation, but never plays it).");
            GrazeSoundId = S(cfg, section, "GrazeSoundId", "donkey_eating_grass", "Sound id for grazing.");
            GrazeVolume = F(cfg, section, "GrazeVolume", 1f, 0f, 3f, "Graze sound volume.");
            LipsVolume = F(cfg, section, "LipsVolume", 1f, 0f, 3f, "Volume of the lip-smacking the game's own donkey animation plays (0 = silent).");
            BraySoundId = S(cfg, section, "BraySoundId", "voice_donkey", "Sound id for brays (the donkey's dialogue voice: short ee-aw sounds).");
            BrayOnMountChance = F(cfg, section, "BrayOnMountChance", 0.5f, 0f, 1f, "Chance the donkey brays when you mount (0 = never, 1 = always).");
            BrayIdleEverySeconds = F(cfg, section, "BrayIdleEverySeconds", 40f, 0f, 300f, "On average one bray this often while standing (randomised by half either way). 0 = no idle brays.");
            BrayWhileWalking = B(cfg, section, "BrayWhileWalking", false, "Idle brays can also happen while moving.");
            BrayVolume = F(cfg, section, "BrayVolume", 1f, 0f, 3f, "Bray volume.");
            BrayPitch = F(cfg, section, "BrayPitch", 1f, 0.3f, 2f, "Bray pitch.");
            BrayInSfxChannel = B(cfg, section, "BrayInSfxChannel", true, "Route brays through the game's SFX volume. Off: the dialogue-voice channel the game uses for this sound (speech volume).");
            CargoThumpOn = B(cfg, section, "CargoThumpOn", true, "Cart: a thump when an item lands in the cart (on top of the game's pickup sound).");
            CargoThumpSoundId = S(cfg, section, "CargoThumpSoundId", "oh_wood_container_drop", "Sound id for an item landing in the cart.");
            CargoThumpVolume = F(cfg, section, "CargoThumpVolume", 1f, 0f, 3f, "Cargo thump volume.");
            CargoThumpPitch = F(cfg, section, "CargoThumpPitch", 1f, 0.3f, 2f, "Cargo thump pitch.");
            // Getting on and off, and hitching a cart: the game has no riding, so these borrow its sounds (picked by ear,
            // 2026-10-05): with the cart the ladder's climb start and finish, the donkey alone its two lip snorts (an id
            // with "#n" plays only that clip of the sound), hitching the tool equip.
            MountOn = B(cfg, section, "MountOn", true, "A sound when the keeper gets on the donkey (in this style).");
            MountSoundId = S(cfg, section, "MountSoundId", cart ? "ladder_climb_start" : "donkey_lips#1", "Sound id for getting on. id#n plays only clip n of that sound.");
            MountVolume = F(cfg, section, "MountVolume", 1f, 0f, 3f, "Getting-on volume.");
            MountPitch = F(cfg, section, "MountPitch", 1f, 0.3f, 2f, "Getting-on pitch.");
            // The lip snorts open with 0.44 s of silence (made to line up with the donkey's animation; measured
            // 2026-10-05), which felt late on a key press: the clip starts that far in.
            MountSoundStart = F(cfg, section, "MountSoundStart", cart ? 0f : 0.42f, 0f, 1f, "Start the getting-on clip this many seconds in (skips silence at its start).");
            DismountOn = B(cfg, section, "DismountOn", true, "A sound when the keeper gets off the donkey (in this style; not at doors, where he is already elsewhere).");
            DismountSoundId = S(cfg, section, "DismountSoundId", cart ? "ladder_climb_finish" : "donkey_lips#2", "Sound id for getting off. id#n plays only clip n of that sound.");
            DismountVolume = F(cfg, section, "DismountVolume", 1f, 0f, 3f, "Getting-off volume.");
            DismountPitch = F(cfg, section, "DismountPitch", 1f, 0.3f, 2f, "Getting-off pitch.");
            DismountSoundStart = F(cfg, section, "DismountSoundStart", cart ? 0f : 0.42f, 0f, 1f, "Start the getting-off clip this many seconds in (skips silence at its start).");
            HitchOn = B(cfg, section, "HitchOn", true, "Cart: a sound when a donkey is hitched to a cart.");
            HitchSoundId = S(cfg, section, "HitchSoundId", "equip_tool", "Sound id for hitching a cart.");
            HitchVolume = F(cfg, section, "HitchVolume", 1f, 0f, 3f, "Hitching volume.");
            HitchPitch = F(cfg, section, "HitchPitch", 1f, 0.3f, 2f, "Hitching pitch.");
            UnhitchOn = B(cfg, section, "UnhitchOn", true, "Cart: a sound when a donkey leaves its cart standing.");
            UnhitchSoundId = S(cfg, section, "UnhitchSoundId", "unequip_tool", "Sound id for leaving the cart standing (the counterpart of the hitching sound).");
            UnhitchVolume = F(cfg, section, "UnhitchVolume", 1f, 0f, 3f, "Unhitching volume.");
            UnhitchPitch = F(cfg, section, "UnhitchPitch", 1f, 0.3f, 2f, "Unhitching pitch.");
            WheelOn = B(cfg, section, "WheelOn", cart, "Cart: a wooden wheel rumble once per walk cycle while the cart moves (the game has no cart sound; this borrows a machine wheel).");
            WheelSoundId = S(cfg, section, "WheelSoundId", "conveyor_furnace_wheel", "Sound id for the wheels (conveyor_assembly_wheel is the chain-and-rev alternative).");
            WheelVolume = F(cfg, section, "WheelVolume", 0.6f, 0f, 3f, "Wheel volume.");
            WheelPitch = F(cfg, section, "WheelPitch", 1f, 0.3f, 2f, "Wheel pitch.");
            WheelPitchFollowsSpeed = B(cfg, section, "WheelPitchFollowsSpeed", false, "Raise the wheel pitch with the walk animation speed (faster = higher).");
            WheelEveryCycles = I(cfg, section, "WheelEveryCycles", 1, 1, 4, "Play the wheel sound every this many walk cycles.");
            WheelPhase = F(cfg, section, "WheelPhase", 0f, 0f, 1f, "Shifts the wheel sound within its cycle.");
            BellSoundId = S(cfg, section, "BellSoundId", "donkey_bell", "Sound id for the bell (the donkey delivery's bell).");
            BellOnSetOff = B(cfg, section, "BellOnSetOff", cart, "Ring the bell when the donkey sets off after standing still a while.");
            BellOnlyWithCargo = B(cfg, section, "BellOnlyWithCargo", true, "Only ring on setting off when there is cargo on the cart.");
            BellSetOffIdleSeconds = F(cfg, section, "BellSetOffIdleSeconds", 4f, 0f, 60f, "How long the donkey must have stood still before setting off rings the bell.");
            BellVolume = F(cfg, section, "BellVolume", 0.7f, 0f, 3f, "Bell volume.");
            BellPitch = F(cfg, section, "BellPitch", 1f, 0.3f, 2f, "Bell pitch.");
        }

        private static ConfigEntry<bool> B(ConfigFile cfg, string section, string key, bool def, string desc)
        {
            return cfg.Bind(section, key, Defaults.Get(section, key, def), desc);
        }

        private static ConfigEntry<string> S(ConfigFile cfg, string section, string key, string def, string desc)
        {
            return cfg.Bind(section, key, Defaults.Get(section, key, def), desc);
        }

        private static ConfigEntry<float> F(ConfigFile cfg, string section, string key, float def, float min, float max, string desc)
        {
            return cfg.Bind(section, key, Defaults.Get(section, key, def), new ConfigDescription(desc, new AcceptableValueRange<float>(min, max)));
        }

        private static ConfigEntry<int> I(ConfigFile cfg, string section, string key, int def, int min, int max, string desc)
        {
            return cfg.Bind(section, key, Defaults.Get(section, key, def), new ConfigDescription(desc, new AcceptableValueRange<int>(min, max)));
        }

        // Every sound entry, for the Sounds tab's reset and the defaults export.
        public ConfigEntryBase[] SoundEntries => new ConfigEntryBase[]
        {
            SoundsOn, SoundVolume, HoofOn, HoofSoundId, HoofSurfaceAware, HoofVolume, HoofPitch, HoofPitchVariation, HoofStepsPerCycle, HoofPhase,
            GrazeOn, GrazeSoundId, GrazeVolume, LipsVolume,
            BraySoundId, BrayOnMountChance, BrayIdleEverySeconds, BrayWhileWalking, BrayVolume, BrayPitch, BrayInSfxChannel,
            CargoThumpOn, CargoThumpSoundId, CargoThumpVolume, CargoThumpPitch,
            MountOn, MountSoundId, MountVolume, MountPitch, MountSoundStart,
            DismountOn, DismountSoundId, DismountVolume, DismountPitch, DismountSoundStart,
            HitchOn, HitchSoundId, HitchVolume, HitchPitch,
            UnhitchOn, UnhitchSoundId, UnhitchVolume, UnhitchPitch,
            WheelOn, WheelSoundId, WheelVolume, WheelPitch, WheelPitchFollowsSpeed, WheelEveryCycles, WheelPhase,
            BellSoundId, BellOnSetOff, BellOnlyWithCargo, BellSetOffIdleSeconds, BellVolume, BellPitch,
        };

        public void ResetSounds()
        {
            foreach (ConfigEntryBase e in SoundEntries)
            {
                Defaults.ResetToDefault(e);
            }
        }

        // Every setting of this style, in the order defaults.cfg lists them.
        public ConfigEntryBase[] AllEntries
        {
            get
            {
                var list = new System.Collections.Generic.List<ConfigEntryBase>
                {
                    SpeedMultiplier, WalkCycleSpeed, FocusSpeed, Depth, CenterOffset, RiderOffsetPx, TorsoOffsetPx,
                    AutoSeat, SeatAdjustPx, SeatOffset, Bob, Pose, FreezePoseWhenIdle, PoseSpeed, PoseSpeedFollowsDonkey,
                    NearLeg, LegSource, LegHeightPx, LegAdjustXPx, LegAdjustYPx, BackSeatExtraPx, SnapToGround, CartFollowsGround, CartGroundMaxShift, CartSlope, CartPitchMaxDegrees,
                    CargoDrawCorpse, CargoSwaysWithCart, CargoSetDownBehindCart, CargoSetDownGap, CargoBedDepth, CargoSideGap, CargoOverhang, CargoNestle, CargoLayerShift,
                    LanternAlong, LanternUp, LanternAcross, LanternTilt, LanternLean, LanternFlip, LanternHangs,
                    LanternSwayDegrees, LanternSwaySeconds, IdleFidgetEverySeconds, IdleFidgetSpeed, IdleCrawlSpeed,
                    BobScale, BobLagFrames, HandsSteady, HandsSwayPx, HeadLagFrames, HeadSettle,
                    LanternShow, LanternScale, LanternLightOn, LanternLightIntensity, LanternLightRange, LanternLightColor, LanternLightShadows, LanternLightDayDim,
                    LanternSelfLightOn, LanternSelfLightIntensity, LanternSelfLightRange, LanternGlowOn, LanternGlowAlpha, LanternGlowScale, LanternGlassOn,
                };
                list.AddRange(SoundEntries);
                return list.ToArray();
            }
        }

        // Back to the mod defaults (Defaults.DefaultFor: the defaults file as last written, not only as built).
        public void ResetAll()
        {
            foreach (ConfigEntryBase e in AllEntries)
            {
                Defaults.ResetToDefault(e);
            }
        }
    }

    // A rideable mount for Graveyard Keeper 2. Press the mount key and a donkey appears under the keeper; you walk
    // faster while riding. The donkey is a temporary view only (the game's build-preview mechanism): it is never
    // added to the world data, so nothing about it is saved. See README.md and FEASIBILITY.md.
    [BepInPlugin(Guid, "GK2 Donkey API", Version)]
    // Soft: with GK2 Mod Framework installed, player mods' settings pages go into its Mods menu (FrameworkLink);
    // without it they go into the game's own Settings window. Never needed.
    [BepInDependency(FrameworkLink.FrameworkGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "dreas.gk2.donkeyapi";
        // The one version of this mod: the plugin's and the API's (Mounts.ApiVersion is read from it). Minor for additions
        // to the API, major for breaking changes; the public and the internal number never differ (decided 2026-10-13).
        public const string Version = "0.36.0";

        // No pinned game build: at start GameCode compares every game member this DLL uses (the gk2deps.txt manifest
        // tools/DepManifest embedded after the build) with the installed game, feature by feature; docs/dependency-check.md.

        internal static ManualLogSource Log;

        // Set when the application starts closing (Application.quitting runs before the plugin's OnDestroy).
        internal static bool Quitting;
        internal static Plugin Instance;

        // Runs once, at the top of the next LateUpdate: a settings page's hand-over to the menus (MountSettingsPage.Publish),
        // which has to wait until every plugin's Awake has run (they all run in the chainloader's loop, before any frame).
        private static readonly System.Collections.Generic.Queue<Action> nextFrame = new System.Collections.Generic.Queue<Action>();

        internal static void NextFrame(Action action)
        {
            if (action != null) nextFrame.Enqueue(action);
        }

        // [General]: behaviour
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<string> MountWgoId;
        // Ride behaviour: the core's defaults, overridable per session through Mounts.Behaviour (Saddle Up sets
        // them from its own settings). No keys here: the core mounts only when a mod asks (Mounts.TryMount).
        internal static Tuned<bool> StayMountedThroughDoors;
        internal static Tuned<bool> DismountIndoors;
        internal static Tuned<bool> CartIndoors;
        internal static Tuned<bool> FlyIndoors;
        internal static Tuned<bool> ShowMessages;
        internal static Tuned<bool> CargoZombies;
        internal static Tuned<bool> CargoChest;
        internal static Tuned<int> CartSlots;
        internal static Tuned<bool> CorpseIntoMorgue;
        internal static Tuned<string> CorpseIntoMorguePlaces;
        internal static ConfigEntry<bool> InstanceControls;
        internal static Tuned<KeyCode> HitchKey;
        internal static Tuned<KeyboardShortcut> JumpKey, FlyKey;
        internal static Tuned<bool> FallGuard;              // never lost: the fall watchdog (SafeSpots.cs)
        internal static Tuned<KeyboardShortcut> ReturnKey;  // never lost: back to the last safe spot by a key
        internal static Tuned<bool> StandingSounds;         // standing mounts and babies make their sounds near the keeper (Sounds.cs)

        // Every [HarmonyPatch] class (tests/FrameworkScan checks the list is complete).
        internal static readonly Type[] PatchClasses =
        {
            typeof(PlayerData_AddOverheadItem_Patch),
            typeof(BigDropInteractionHandler_HasInteraction_Patch),
            typeof(PlayerInteractionComponent_CanInteractWithDrop_Patch),
            typeof(ZombieInteractionHandler_HasInteraction_Patch),
            typeof(ZombieInteractionHandler_FormInteractionInfo_Patch),
            typeof(ZombieStations_TakeBack_Patch),
            typeof(ChestInteractionHandler_Interact_Patch),
            typeof(ChestInteractionHandler_HasInteraction2_Patch),
            typeof(PlayerController_Teleport_Patch),
            typeof(PlayerController_Teleport_DoorNote_Patch),
            typeof(WorkPlayerState_CanEnter_Patch), // no work state while the mount flies (Flight.cs)
            typeof(Projectile_Launch_Patch), // the arrow leaves the drawn bow while mounted (ArrowOrigin.cs)
            typeof(WorldZoneData_TryAddWgoData_Patch),
            typeof(GameSave_PrepareToSave_Patch),
            typeof(ChestInteractionHandler_Interact2_Patch),
            typeof(ChestInteractionHandler_FormInteractionInfo_Patch),
            typeof(PlayerData_HasFreeOverheadSlot_Patch),
            typeof(PlayerData_TryGetOverheadItem_Patch),
            typeof(PlayerData_RemoveOverheadItem_Patch),
            typeof(PlayerData_InsertOverheadItemTo_Patch),
            typeof(PlayerInputHandler_UpdateInput_Patch),
            typeof(LazyExpressionEvaluationScope_GetOverheadItemForMutation_Patch),
            typeof(AnimationComponentBase_OnAnimationStepCompleted_Patch),
            typeof(AnimationComponentBase_PlaySound_Patch),
            typeof(AnimationComponentBase_SetDirection_Patch),
            typeof(AnimationComponentBase_SetState_Patch),
            typeof(AnimationComponentBase_SetDirectionEnum_Patch),
            typeof(AnimationComponentBase_SetTrigger_Patch),
            typeof(WgoDataScriptsManager_CreateScript_Patch),
            typeof(Flow_Body_DoAction_Patch),
            typeof(Wgo_CompleteVisualPartsLoad_Patch),
            typeof(Wgo_OnDestroy_Patch),
            typeof(SaveSystem_Save_Patch),
            typeof(SaveSystem_Remove_Patch),
            typeof(PlayerPhysicalBody_LockMovement_Patch), // the flying experiment: a movement unlock keeps a flying body kinematic (Flight.cs)
            typeof(PlayerController_SetPosition_Patch), // never lost: a placement of the keeper starts the fall watchdog's clocks anew (SafeSpots.cs)
            typeof(LazyTerrainMeshCollection_Runtime_StripCpuMeshData_Patch), // the flying experiment: the drawn ground's tiles (DrawnGround.cs)
            typeof(LazyTerrain_Awake_Patch), typeof(LazyTerrain_OnDestroy_Patch), // and its index, kept as the terrains come and go
        };
        internal static ConfigEntry<float> CartZoneSide, CartZoneRear, CartZoneFrontShift, DonkeyZoneAhead;

        // [Ride] / [Cart]: position, pose, animation, legs, speed
        internal static StyleSettings Ride;
        internal static StyleSettings Cart;
        internal static StyleSettings For(MountStyle style) => style == MountStyle.Cart ? Cart : Ride;

        // [Debug]
        internal static ConfigEntry<bool> TuningKeys;
        internal static ConfigEntry<string> SelfTestSpot;
        internal static ConfigEntry<bool> CargoTabTint;
        internal static ConfigEntry<bool> SkinPackAutoReload;
        internal static ConfigEntry<bool> MountsPreview;
        internal static ConfigEntry<int> PanelFakeEntries; // dev: made-up entries padded onto the Mounts panel's list, to see it scroll
        // What Debug.MountsPreview said at start (the built-ins register once, so a change takes the next start): the
        // mounts other than the donkey, the skins and the effects are on. Off for players in this version
        // (Mounts.MountsEnabled; the next update switches them on).
        internal static bool MountsOn { get; private set; }
        internal static ConfigEntry<bool> CargoSeatOnShapes;
        internal static ConfigEntry<KeyCode> CargoNudgeUp, CargoNudgeDown;
        internal static ConfigEntry<bool> TuningWindowEnabled;
        internal static ConfigEntry<KeyCode> TuningWindowKey;
        internal static ConfigEntry<KeyCode> ReviewPrevKey, ReviewNextKey, ReviewOkKey, ReviewWrongKey, ReviewLaterKey; // the Cargo tab's review strip
        internal static bool TuningWindowHidden; // toggled by TuningWindowKey; starts as remembered (TuningWindowHiddenCfg); the self-tests hide it for the run only
        internal static ConfigEntry<bool> TuningWindowHiddenCfg; // the hide remembered across sessions: left hidden, it stays hidden after a restart
        internal static ConfigEntry<float> TuningWindowScale;
        internal static ConfigEntry<float> TuningWindowFontScale;
        internal static ConfigEntry<float> TuningWindowWidth;

        // [Safety]: GameCode.cs (StopOnChangedGameCode, ForceRunOnChangedGameCode, LastDependencyCheck)

        private Harmony harmony;

        // True once every patch is in: the API (Mounts.IsAvailable) refuses everything before that and after a
        // game update disabled the mod.
        internal static bool Patched;

        private void Awake()
        {
            try
            {
                Load();
            }
            finally
            {
                // In GK2 Mod Framework's Mods list too, also when it switched itself off (then shown as not confirmed
                // for this build): the mods built on it list it as their dependency (FrameworkLink).
                FrameworkLink.TryRegisterApi(Config);
            }
        }

        // One write of the cfg at the end instead of one per new key: with SaveOnConfigSet on (BepInEx's default) every
        // Bind of a key the file doesn't have yet saves the whole file, and a first start binds about 470 keys into a
        // file that grows past 100 KB (2026-10-13). Afterwards every change is written at once, as before.
        private void Load()
        {
            Config.SaveOnConfigSet = false;
            try
            {
                BindAndPatch();
            }
            finally
            {
                Config.SaveOnConfigSet = true;
                Config.Save();
            }
        }

        private void BindAndPatch()
        {
            Log = Logger;
            Log.LogEvent += (_, __) => Perf.NoteLogLine(); // the panel's log lines per second
            Instance = this;
            Application.quitting += () =>
            {
                Quitting = true;
                // A layout edit is written 0.75 s after the last change, from OnGUI: one made just before quitting is written here.
                try
                {
                    CargoLayout.SaveIfDue(now: true);
                    CargoReview.Flush(); // the review's last verdict too
                }
                catch (Exception e)
                {
                    Log.LogWarning("Cargo layout: saving at quit failed: " + e.Message);
                }
            };
            Enabled = Config.Bind("General", "Enabled", true,
                "Master switch for the mount. GK2 Donkey API does nothing on its own: a mod built on it (such as Saddle Up) summons and dismounts the donkey. Off: no mod can mount (the kill switch if one misbehaves).");
            MountWgoId = Config.Bind("General", "MountWgoId", "npc_donkey",
                "World object id used for the mount's looks. Only npc_donkey has been tested.");
            StayMountedThroughDoors = Config.Bind("General", "StayMountedThroughDoors", Defaults.Get("General", "StayMountedThroughDoors", true),
                "Keep riding after a scene change (the donkey is re-summoned in the new scene). Off: dismount at every door.");
            DismountIndoors = Config.Bind("General", "DismountIndoors", Defaults.Get("General", "DismountIndoors", true),
                "Dismount automatically when entering a building, church or dungeon (the game's indoor lighting presets), and refuse to mount while indoors. The cart cannot avoid furniture and door frames inside.");
            CartIndoors = Config.Bind("General", "CartIndoors", Defaults.Get("General", "CartIndoors", false),
                "With DismountIndoors off (riding indoors allowed): the cart comes indoors too. Off: riding indoors is allowed, the cart is not (driving it through a door into a building leaves it standing at the door with its load and the ride goes on inside; at the morgue with a corpse aboard the keeper gets off inside carrying it; the cart can't be called, hitched or switched to indoors). Ignored while DismountIndoors is on.");
            FlyIndoors = Config.Bind("General", "FlyIndoors", Defaults.Get("General", "FlyIndoors", false),
                "A flying mount may take off and fly indoors (there the flight is a lift only, never through walls). Off: no take-off indoors, and a flight that goes through a door into a building lands there.");
            ShowMessages = Config.Bind("General", "ShowMessages", Defaults.Get("General", "ShowMessages", true),
                "Show a thought bubble when the keeper cannot mount and why.");
            CargoZombies = Config.Bind("General", "CargoZombies", Defaults.Get("General", "CargoZombies", true),
                "Zombies ride on the cart like any other load (as many as it holds), and can be handed to their work straight from it. Off: the cart refuses zombies, and they can't be taken while riding.");
            CargoChest = Config.Bind("General", "CargoChest", Defaults.Get("General", "CargoChest", true),
                "A plain storage chest (rough, normal or good) rides on the cart: the action key on it while driving lifts it, contents and all; the interact key with nothing in front sets it down. Nothing else rides with it. Off: chests stay where they are.");
            CartSlots = Config.Bind("General", "CartSlots", Defaults.Get("General", "CartSlots", 6),
                new ConfigDescription("How many items the cart carries: 2 like the game's own delivery cart, 4 (two layers), 6 (three layers). Applied to every cart at once. A cart holding more than the new number stays full until it is unloaded; nothing is ever dropped. A chest aboard still takes the whole cart.", new AcceptableValueRange<int>(1, 6)));
            CartSlots.Entry.SettingChanged += (_, __) => Mount.ApplyCartSlots("the setting changed");
            CorpseIntoMorgue = Config.Bind("General", "CorpseIntoMorgue", Defaults.Get("General", "CorpseIntoMorgue", true),
                "Driving the cart through the morgue door with a corpse aboard: the ride ends at the door as always and the cart stays outside with its load, but the corpse goes into the keeper's hands, so he walks in carrying it (one; more with the game's extra hand slots). Only there (CorpseIntoMorguePlaces). Off: it stays on the cart.");
            CorpseIntoMorguePlaces = Config.Bind("General", "CorpseIntoMorguePlaces", Defaults.Get("General", "CorpseIntoMorguePlaces", "morgue, tp_RT_morgue_exit, tp_morgue_to_graveyard"),
                "Where CorpseIntoMorgue applies, comma-separated: building area ids (the game's world zones; the keeper's area after the door) and door destinations (the teleport point ids the game's doors pass). Either one matching is enough. Empty: nowhere.");
            InstanceControls = Config.Bind("General", "InstanceControls", Defaults.Get("General", "InstanceControls", true),
                "Donkeys and carts a mod left standing in the world (Mounts.SpawnDonkey / SpawnCart): the interact key next to a standing donkey rides it (empty hands), and next to a free cart while riding a donkey without its cart hitches it; a label over it says so. Off: only mods can do that (MountHandle.Ride, Mounts.Hitch).");
            HitchKey = Config.Bind("General", "HitchKey", Defaults.Get("General", "HitchKey", KeyCode.J),
                "With InstanceControls, while riding: leave the cart standing where it is (a free cart with its cargo and settings), or take the free cart in reach. None: the key is off (the interact key still hitches).");
            JumpKey = Config.Bind("General", "JumpKey", Defaults.Get("General", "JumpKey", new KeyboardShortcut(KeyCode.Space)),
                "Riding plain: the mount jumps (the flying experiment, dev only for now). The same key as FlyKey: a second press in the air takes off on a mount that flies. None: no jumping. In a fight (in armour) Space is the game's attack, and isn't read here.");
            FlyKey = Config.Bind("General", "FlyKey", Defaults.Get("General", "FlyKey", new KeyboardShortcut(KeyCode.Space)),
                "Riding plain a mount that flies: take off and land (the flying experiment, dev only for now). The same key as JumpKey: take off with a second press in the air during a jump. None: no flying by key.");
            FallGuard = Config.Bind("General", "FallGuard", Defaults.Get("General", "FallGuard", true),
                "If the keeper ever falls out of the world (under the ground, into nothing, far below it) he is put back on the last ground he stood on, with a warning in the log (docs/never-lost.md). Never during a door, a cutscene or a dialogue. Off: nothing watches (the record and the key still work).");
            ReturnKey = Config.Bind("General", "ReturnKey", Defaults.Get("General", "ReturnKey", KeyboardShortcut.Empty),
                "A key that puts the keeper back on the last safe spot of this scene by hand (stuck somewhere). None: no key. Not during a door, a cutscene or a dialogue.");
            StandingSounds = Config.Bind("General", "StandingSounds", Defaults.Get("General", "StandingSounds", true),
                "Standing mounts and babies near you make their sounds: steps, grazing, the lip smack (at the mount sounds' volume). Off: only the mount you ride sounds, and a standing one's lip smack is the game's own.");
            // The on-foot zones of a standing cart (docs/load-on-foot.md), measured from the cart body's drawn box.
            CartZoneSide = Config.Bind("General", "CartZoneSide", Defaults.Get("General", "CartZoneSide", 0.6f),
                new ConfigDescription("On foot next to a standing cart: how far beside the cart body's sides the cart's area reaches (and the donkey's, which is as wide), world units.", new AcceptableValueRange<float>(0f, 2f)));
            CartZoneRear = Config.Bind("General", "CartZoneRear", Defaults.Get("General", "CartZoneRear", 0.8f),
                new ConfigDescription("On foot next to a standing cart: how far behind the cart body's back end the cart's area reaches, world units.", new AcceptableValueRange<float>(0f, 2f)));
            CartZoneFrontShift = Config.Bind("General", "CartZoneFrontShift", Defaults.Get("General", "CartZoneFrontShift", 0f),
                new ConfigDescription("On foot next to a donkey with its cart: where the donkey's area ends and the cart's begins, relative to the cart body's front edge, world units (positive: further back, so more of the cart's side counts as the donkey; negative: towards the donkey).", new AcceptableValueRange<float>(-1f, 1f)));
            DonkeyZoneAhead = Config.Bind("General", "DonkeyZoneAhead", Defaults.Get("General", "DonkeyZoneAhead", 1.2f),
                new ConfigDescription("On foot next to a standing donkey with its cart: how far ahead of the donkey's body its area reaches, world units.", new AcceptableValueRange<float>(0.2f, 2.5f)));

            // [Corpses]: the game's donkey leaves its bodies at the crossroads (Corpses.cs, docs/corpse-delivery.md).
            // Off in the core; a mod switches it on for its player (Mounts.Corpses).
            Corpses.AtCrossroads = Config.Bind(Corpses.Section, "CorpsesAtCrossroads", Defaults.Get(Corpses.Section, "CorpsesAtCrossroads", false),
                "The game's donkey leaves the bodies it brings at the crossroads by the nun, where it appears, instead of putting them on the morgue pallets; you carry or cart them to the morgue yourself. Off: the game's own delivery, nothing of it hooked.");
            Corpses.UnloadAtCrossroads = Config.Bind(Corpses.Section, "CorpsesUnloadAtCrossroads", Defaults.Get(Corpses.Section, "CorpsesUnloadAtCrossroads", true),
                "The donkey walks out from behind its tree to CorpsesStandPoint, unloads there and walks back to vanish. Off: it walks to the morgue as in the game; the bodies still appear at CorpsesDropPoint when it arrives. While a quest keeps the donkey at the morgue for a talk, it walks there either way.");
            Corpses.StandPoint = Config.Bind(Corpses.Section, "CorpsesStandPoint", Defaults.Get(Corpses.Section, "CorpsesStandPoint", "donkey_01_01"),
                "GD point the donkey walks to and unloads at. donkey_01_01 is the road beside the nun; crossroad_nun_corpses right beside her; donkey_02 further west; donkey_01 is its own spawn point behind the tree (then it unloads on the spot after CorpsesUnloadDelay).");
            Corpses.DropPoint = Config.Bind(Corpses.Section, "CorpsesDropPoint", Defaults.Get(Corpses.Section, "CorpsesDropPoint", "nun_body_drop_point"),
                "GD point the bodies are left at. nun_body_drop_point is the game's own spot by the nun; crossroad_nun_corpses right beside her; donkey_01 where the donkey appears.");
            Corpses.NeverRot = Config.Bind(Corpses.Section, "CorpsesNeverRot", Defaults.Get(Corpses.Section, "CorpsesNeverRot", false),
                "A body on the ground normally turns into a grave mound after about 75 minutes of play out of sight (the game's rule). On: the bodies the donkey leaves never rot.");
            Corpses.Notification = Config.Bind(Corpses.Section, "CorpsesNotification", Defaults.Get(Corpses.Section, "CorpsesNotification", "The donkey left a body at the crossroads"),
                "Shown instead of the game's \"New Body\" notification for each body left, with the body's icon. Empty: the game's own.");
            Corpses.UnloadDelay = Config.Bind(Corpses.Section, "CorpsesUnloadDelay", Defaults.Get(Corpses.Section, "CorpsesUnloadDelay", 1.5f),
                new ConfigDescription("Only when the donkey unloads on the spot (CorpsesStandPoint is its spawn point, or no walk is possible): seconds before the bodies appear.", new AcceptableValueRange<float>(0.1f, 10f)));
            Corpses.LeaveDelay = Config.Bind(Corpses.Section, "CorpsesLeaveDelay", Defaults.Get(Corpses.Section, "CorpsesLeaveDelay", 1f),
                new ConfigDescription("Only when the donkey unloads on the spot: seconds it stays before it vanishes.", new AcceptableValueRange<float>(0.1f, 10f)));


            Ride = new StyleSettings(Config, "Ride",
                speed: 1.75f, depth: 0.32f, center: 0.48f, riderPx: -4, torsoPx: -3,
                autoSeat: false, seatAdjustPx: -6, seatOffset: 0.32f, bob: true, pose: RiderPose.FishingIdle, freeze: true,
                legs: false, legSource: LegSource.Body, legHeight: 15, legX: 3, legY: 1);
            Cart = new StyleSettings(Config, "Cart",
                speed: 1.5f, depth: 0f, center: 0f, riderPx: 0, torsoPx: 0,
                autoSeat: false, seatAdjustPx: 0, seatOffset: 0f, bob: false, pose: RiderPose.Walk, freeze: false,
                legs: false, legSource: LegSource.Body, legHeight: 10, legX: 0, legY: 0);
            // [Colors] and [Colors.<region>]: the same donkey in both styles, so one set (Colors.cs).
            Colors.Bind(Config);
            foreach (ConfigEntryBase e in Colors.AllEntries())
            {
                Tuned.MarkLook(e);
            }
            Tuned.MarkLook(Cart.LanternLightColor);
            Tuned.MarkLook(Ride.LanternLightColor);
            // [Look]: the skin a donkey wears (Looks.cs); the built-in skins are registered here too.
            Catalog.Bind(); // the donkey mount first: the skins name theirs
            Looks.Bind(Config);
            // [Effects]: particle effects on donkeys (Effects.cs); Hellfire is registered here.
            Effects.Bind(Config);
            // General.IdleShuffle and [Shuffle]: standing donkeys without a cart take a few slow steps (IdleShuffle.cs).
            IdleShuffle.Bind(Config);

            // The check of the game's code (GameCode.cs), before the [Debug] keys bind: a change in the members the dev
            // tools use locks them for the session. Its verdict is applied after every key is bound (GameCode.Apply).
            GameCode.Bind(Config);
            GameCode.Check();
            // The [Debug] keys: in the cfg only with the dev tools unlocked (DevTools.cs), else in a scratch config nobody sees.
            ConfigFile dbg = DevTools.DebugConfig(Config, GameCode.DevToolsChanged ? "the game changed code the dev tools use" : null);
            TuningWindowEnabled = dbg.Bind("Debug", "TuningWindow", false,
                "Dev tool for mod authors: a tuning panel for every mount value in the top-right corner while mounted, plus the investigation tools on its Debug tab (and F5). Values apply live and are saved to this file as you change them.");
            TuningWindowKey = dbg.Bind("Debug", "TuningWindowKey", KeyCode.F11,
                "Hides or shows the tuning panel, e.g. for screenshots. Remembered (TuningWindowHidden): left hidden, it stays hidden after a restart. The game doesn't use F11 on its own (it can't be bound in its controls; Shift+F11 is its Workshop creator). None = no key.");
            TuningWindowHiddenCfg = dbg.Bind("Debug", "TuningWindowHidden", false,
                "Whether the tuning panel is hidden right now; written as TuningWindowKey toggles it, so the panel comes back the way it was left.");
            TuningWindowHidden = TuningWindowHiddenCfg.Value;
            TuningWindowScale = dbg.Bind("Debug", "TuningWindowScale", 1.5f,
                "Overall size of the tuning panel (1 = Unity default, 1.5 suits 1440p). Text and width scale with it.");
            TuningWindowFontScale = dbg.Bind("Debug", "TuningWindowFontScale", 1f,
                "Text size inside the tuning panel, relative (1 = normal). Lower it so long rows fit.");
            TuningWindowWidth = dbg.Bind("Debug", "TuningWindowWidth", 470f,
                "Width of the tuning panel in panel units before the overall size is applied (470 = original).");
            CargoNudgeUp = dbg.Bind("Debug", "CargoNudgeUp", KeyCode.KeypadPlus, "Cargo tab: moves the last slider dragged up by 0.01 (Shift: 0.05).");
            CargoNudgeDown = dbg.Bind("Debug", "CargoNudgeDown", KeyCode.KeypadMinus, "Cargo tab: moves the last slider dragged down by 0.01 (Shift: 0.05).");
            ReviewPrevKey = dbg.Bind("Debug", "ReviewPrevKey", KeyCode.Keypad4, "Cargo tab, review strip: the previous load.");
            ReviewNextKey = dbg.Bind("Debug", "ReviewNextKey", KeyCode.Keypad6, "Cargo tab, review strip: the next load.");
            ReviewOkKey = dbg.Bind("Debug", "ReviewOkKey", KeyCode.Keypad1, "Cargo tab, review strip: this load looks right.");
            ReviewWrongKey = dbg.Bind("Debug", "ReviewWrongKey", KeyCode.Keypad3, "Cargo tab, review strip: this load is wrong.");
            ReviewLaterKey = dbg.Bind("Debug", "ReviewLaterKey", KeyCode.Keypad2, "Cargo tab, review strip: later.");
            CargoSeatOnShapes = Config.Bind("General", "CargoSeatOnShapes", true,
                "Upper layers of cargo rest on the items' measured shapes (cargo-shapes.cfg) instead of their boxes: a layer on a round log or a zombie sits on it, not on its box. Tuned contacts keep their values. Off: the boxes only.");
            CargoSeatOnShapes.SettingChanged += (_, __) => Cargo.RefreshViews();
            CargoTabTint = dbg.Bind("Debug", "CargoTabTint", true,
                "The panel's Cargo tab tints the items on the ridden cart like the names in it: blue the first item of a layer, orange the second (or one alone on top), lighter the higher the layer. Off: no tint.");
            SkinPackAutoReload = dbg.Bind("Debug", "SkinPackAutoReload", false,
                "A skin pack in use is read again by itself when its files change on disk (skin.cfg or a picture; checked once a second, after the files have stood still for a second), as the Mounts tab's \"Reload from the file\" does. For drawing a skin: save in the editor, look in the game (docs/sprite-tools.md). Off: only the button reloads.");
            SelfTestSpot = dbg.Bind("Debug", "SelfTestSpot", "",
                "Where the self-tests start, as scene|x,y,z. Written by the runner itself after a full run (every scenario) with no failures and no warnings; at the start of a run in that scene the keeper is moved there and put back where he stood afterwards. Empty: the tests run from where he stands (a built-up area with open ground around).");
            TuningKeys = dbg.Bind("Debug", "TuningKeys", false,
                "While mounted: keypad 8/2 move the torso, keypad 4/6 the legs sideways, Insert/Delete the legs up/down, one art pixel per press, editing the current style's values. Development aid, turn off for normal play.");
            // The mounts other than the donkey, the skins and the effects are in this DLL but off for players in this
            // version (the next update switches them on; CHANGELOG "switched on in the next update"). A dev key, so a
            // player's cfg can't flip it: locked, it answers false. Nothing of it is registered when off, so every list
            // (the Mount and Skin rows, the colours per mount) holds the donkey alone and a mod's registrations are
            // kept out (Mounts.KeptOut). The built-ins register here rather than in Looks.Bind / Effects.Bind because
            // the key binds after them.
            MountsPreview = dbg.Bind("Debug", "MountsPreview", false,
                "Dev: switches on what the next update brings players: the mounts other than the donkey (Saddle Up's five: the red dragon, the giant rat, the giant snail, the giant worm, Gerry the skull; and any mod's or skin pack's), the skins (Hellfire with its fire, Royal Parade, the skin packs) and the particle effects. Off, as players have it in this version: only the donkey is offered and a mod's registrations are kept out with a log line. Takes effect at the next start.");
            PanelFakeEntries = dbg.Bind("Debug", "MountsPanelFakeEntries", 0,
                "Dev only: this many made-up entries padded onto the Mounts panel's list (\"Test mount 1\"...), to see it scroll with a long list; they can't be picked. 0 = none. The settings self-test counts them as a failure: 0 for a run.");
            MountsOn = MountsPreview.Value;
            if (MountsOn)
            {
                Looks.RegisterBuiltIns(); // the loose packs; the Hellfire effect is Saddle Up's too (its Awake registers it through the API)
                Log.LogInfo("Mounts, skins and effects: on (Debug.MountsPreview; players have them off in this version).");
            }
            else
            {
                Log.LogInfo("Mounts, skins and effects: off in this version, the donkey is the only mount offered (they come in the next update" + (DevTools.Unlocked ? "; Debug.MountsPreview switches them on here" : "") + ").");
            }

            if (DevTools.Unlocked)
            {
                Log.LogInfo("Dev tools unlocked by " + DevTools.How + ": the [Debug] keys are in the cfg (Debug.TuningWindow is the panel).");
            }
            CargoLayout.Load(); // cargo-layout.cfg: the repo file on the machine with the sources, else the embedded copy
            CargoShapes.Load(); // cargo-shapes.cfg: the measured item shapes, the same way
            Log.LogInfo("Cargo layout: " + CargoLayout.Summary());

            // Identical game members: patch. Changed ones: by feature (GameCode.cs: stop, freeze, warn, lock the dev tools).
            // A member gone, or a check that could not run: stop; the game runs unmodified.
            if (!GameCode.Apply())
            {
                return;
            }

            harmony = new Harmony(Guid);
            try
            {
                Access.Verify();
                // By class, never PatchAll(Assembly): that loads every type of the DLL, and without GK2 Mod Framework the
                // one deriving from its base class (FrameworkLink) can't load, so Harmony would warn at every start.
                foreach (Type patch in PatchClasses)
                {
                    if (GameCode.SkipsPatch(patch))
                    {
                        continue; // a frozen feature's patch (GameCode): the game's code is not hooked there at all
                    }
                    harmony.PatchAll(patch);
                }
            }
            catch (Exception e)
            {
                harmony.UnpatchSelf();
                harmony = null;
                Log.LogError($"Patching failed; all patches removed, game runs unmodified. {e}");
                GameCode.StoppedBy("patching failed (see BepInEx/LogOutput.log)");
                return;
            }
            Patched = true;
            // The corpse delivery's patch targets (its patches go in only when a mod switches the feature on): a
            // missing one switches that feature off for good, not the API.
            try
            {
                Corpses.Verify();
            }
            catch (Exception e)
            {
                Corpses.Unavailable(e.Message);
            }
            // With the dev panel on (Debug.TuningWindow), the [perf] line is written every 10 s from the start: a
            // measurement no longer depends on remembering the panel's switch (it can still be switched off there).
            Perf.LogEvery10s = TuningWindowEnabled.Value;
            Perf.AllocBySection = TuningWindowEnabled.Value;
            MainGame.OnGameStarted = (Action)Delegate.Combine(MainGame.OnGameStarted, new Action(Mount.OnGameStarted));
            PlayerController.OnPlayerTeleported += Mount.OnKeeperTeleported; // foals indoors (FoalsIndoors.cs)
            Log.LogInfo($"GK2 Donkey API {Version} loaded; nothing happens until a mod uses it.");
        }

        // The flying experiment's free flight moves the keeper's body on the physics step (Flight.FixedStep; nothing
        // while not flying free).
        private void FixedUpdate()
        {
            try { Flight.FixedStep(); }
            catch (Exception e) { Log.LogWarning("[flight] the free flight's step failed: " + e.Message); }
        }

        // LateUpdate: runs after the game moved the player for this frame, so the donkey follows without lag.
        private void LateUpdate()
        {
            // The deferred actions first, whatever state the API is in (a page reaches the menus on a stopped API too).
            while (nextFrame.Count > 0)
            {
                Action a = nextFrame.Dequeue();
                try
                {
                    a();
                }
                catch (Exception e)
                {
                    Log.LogWarning("A deferred action threw (a settings page may be missing from the menus): " + e);
                }
            }
            // Unity's layout pass runs before every OnGUI and makes garbage even when nothing is drawn: off while the
            // panel isn't showing (set here, before this frame's OnGUI, so the events of one frame agree).
            bool gui = harmony != null && Enabled.Value && (TuningWindow.Showing());
            if (useGUILayout != gui)
            {
                useGUILayout = gui;
            }
            if (harmony == null)
            {
                return;
            }
            Corpses.Tick(); // the corpse-delivery patches follow their switch (off: not hooked at all)
            if (!Enabled.Value)
            {
                // Switched off mid-ride: hand the cargo back and restore the keeper instead of leaving a frozen
                // donkey, a raised speed and the cart reroute behind (the patches stay installed until restart).
                if (Mount.IsMounted || Mount.All.Count > 0)
                {
                    Mount.ForceCleanup("disabled in the config");
                }
                return;
            }
            // Investigation: F5 writes the keeper's held-item view to the log (works on foot too, unlike the panel).
            if (TuningWindowEnabled.Value && UnityInput.Current.GetKeyDown(KeyCode.F5))
            {
                HeldItemProbe.Write();
            }
            // Plain key only: Shift+F11 opens the game's Workshop creator (when enabled), so leave that combination to it.
            // Only with the panel switched on: with it off the key did nothing visible but still wrote TuningWindowHidden.
            if (TuningWindowEnabled.Value && TuningWindowKey.Value != KeyCode.None && UnityInput.Current.GetKeyDown(TuningWindowKey.Value)
                && !UnityInput.Current.GetKey(KeyCode.LeftShift) && !UnityInput.Current.GetKey(KeyCode.RightShift))
            {
                TuningWindowHidden = !TuningWindowHidden;
                TuningWindowHiddenCfg.Value = TuningWindowHidden;
            }
            try
            {
                SettingsRows.Tick(); // a key being set in the game's Settings window (before the keys act)
                KeyActions.Poll(); // the mods' keys (MountSettingsPage.Key with pressed:), before this frame's update
            }
            catch (Exception e)
            {
                Log.LogError($"The mods' keys failed this frame. {e}");
            }
            try
            {
                Mount.Tick();
            }
            catch (Exception e)
            {
                Log.LogError($"Mount update failed; dismounting. {e}");
                Mount.ForceCleanup("error");
            }
        }

        private void OnGUI()
        {
            if (harmony == null || !Enabled.Value)
            {
                return;
            }
            try
            {
                if (!useGUILayout)
                {
                    // The panel is off this frame (LateUpdate): no GUILayout without the layout pass.
                    CargoLayout.SaveIfDue(now: !Mount.IsMounted);
                    CargoReview.SaveIfDue(null); // pending marks, 0.75 s after the last (the panel passes the order; this keeps the last one)
                    return;
                }
                using (Perf.Measure(Perf.S.Panel))
                {
                    if (TuningWindow.Showing())
                    {
                        TuningWindow.Draw();
                    }
                }
            }
            catch (Exception e)
            {
                Log.LogWarning("Tuning window failed: " + e.Message);
            }
        }

        private void OnDestroy()
        {
            try
            {
                MountsPanel.SavePickedNow(); // a pick not yet written (the game quit with the panel open)
            }
            catch
            {
            }
            try
            {
                Mount.ForceCleanup("plugin unloaded");
            }
            catch
            {
            }
            Patched = false;
            Looks.Unbind();
            Effects.Unbind();
            Corpses.Remove();
            MainGame.OnGameStarted = (Action)Delegate.Remove(MainGame.OnGameStarted, new Action(Mount.OnGameStarted));
            PlayerController.OnPlayerTeleported -= Mount.OnKeeperTeleported;
            harmony?.UnpatchSelf();
            harmony = null;
        }
    }
}
