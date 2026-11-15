using System;
using System.Collections.Generic;
using System.Reflection;

namespace GK2DonkeyAPI
{
    // Term (a text with a translation of its own: its owner, its key, its English) and Word (one of the owner's own words)
    // are the window library's (GK2DreasUI.Words); a pack's mount or skin name is a Term (Catalog.NameTerm, its pack's
    // file). The window's own words are the library's too, aliased below so every UiWords.X of the API stays.

    // The API's own words (docs/localization.md): the options window, the Mount Journal and its colours window, and the
    // rows its builders add to a page (MountRows' Skin, FoalRows) (step 1, 2026-10-31); the keeper's bubbles, the prompts
    // on foot and the crossroads notification (step 2, 2026-11-01). Key names stay English (D8). The table is complete by
    // construction: the English file lists every field (All), and the game code says no sentence of its own (the
    // release scans look for a Say("...") with a literal).
    internal static class UiWords
    {
        // the options window and the rows' own words: the window library's (GK2DreasUI.Words), the same objects
        internal static readonly Word Done = Words.Done;
        internal static readonly Word Defaults = Words.Defaults;
        internal static readonly Word SetBackAsk = Words.SetBackAsk;
        internal static readonly Word SetBack = Words.SetBack;
        internal static readonly Word Cancel = Words.Cancel;
        internal static readonly Word HintPad = Words.HintPad;
        internal static readonly Word HintMouse = Words.HintMouse;
        internal static readonly Word PressKeyFor = Words.PressKeyFor;
        internal static readonly Word KeyHowPad = Words.KeyHowPad;
        internal static readonly Word KeyHowMouse = Words.KeyHowMouse;
        internal static readonly Word OnlyInGame = Words.OnlyInGame;
        internal static readonly Word NotNow = Words.NotNow;
        internal static readonly Word AndThen = Words.AndThen;
        internal static readonly Word PageOptions = Words.PageOptions;
        internal static readonly Word Open = Words.Open;
        internal static readonly Word Go = Words.Go;
        internal static readonly Word PressAKey = Words.PressAKey;
        internal static readonly Word None = Words.None;
        internal static readonly Word SameKeyAs = Words.SameKeyAs;
        internal static readonly Word ClashesWith = Words.ClashesWith;
        internal static readonly Word NoChoice = Words.NoChoice;

        // the rows the API adds to a page (MountRows, FoalRows)
        internal static readonly Word SkinRow = new Word("row.skin", "Skin");
        internal static readonly Word DefaultSkin = new Word("row.default_skin", "Default", "the Skin list: a mount's own look");
        internal static readonly Word Babies = new Word("row.babies", "Babies");
        internal static readonly Word BabiesFollow = new Word("row.babies_follow", "Babies follow you on foot");
        internal static readonly Word BabiesIndoors = new Word("row.babies_indoors", "Babies come indoors");

        // the Mount Journal
        internal static readonly Word JournalTitle = new Word("journal.title", "Mount Journal");
        internal static readonly Word Search = new Word("journal.search", "Search...", "the search box over the mounts, while empty");
        internal static readonly Word Customize = new Word("journal.customize", "Customize");
        internal static readonly Word Ride = new Word("journal.ride", "Ride");
        internal static readonly Word DriveCart = new Word("journal.drive_cart", "Drive the cart");
        internal static readonly Word Options = new Word("journal.options", "Options");
        internal static readonly Word Stats = new Word("journal.stats", "Stats");
        internal static readonly Word Abilities = new Word("journal.abilities", "Abilities");
        internal static readonly Word StatSpeed = new Word("journal.stat_speed", "Speed");
        internal static readonly Word StatCargo = new Word("journal.stat_cargo", "Cargo", "the Cargo stat's name; its value is a number (how many things the cart holds)");
        internal static readonly Word StatSkins = new Word("journal.stat_skins", "Skins");
        internal static readonly Word StatStatus = new Word("journal.stat_status", "Status");
        internal static readonly Word NoCart = new Word("journal.no_cart", "No cart");
        internal static readonly Word CargoOf = new Word("journal.cargo_of", "{0} of {1}", "the Cargo stat while a mod limits the cart: {0} what the cart holds now, {1} what the mount pulls (numbers)");
        internal static readonly Word Flying = new Word("journal.flying", "Flying");
        internal static readonly Word FlyingLine = new Word("journal.flying_line", "It lands wherever you can stand.");
        internal static readonly Word CargoCart = new Word("journal.cargo_cart", "Cargo cart");
        internal static readonly Word CargoCartLine = new Word("journal.cargo_cart_line", "Pulls a cart (cargo {0}).", "the Cargo cart ability; {0}: how many things the cart holds, a number from 1 to 6");
        internal static readonly Word NotInstalled = new Word("journal.not_installed", "Not installed");
        internal static readonly Word NameNotInstalled = new Word("journal.name_not_installed", "{0} (not installed)", "{0}: a mount's or skin's name, its mod or pack gone");
        internal static readonly Word NameSetAside = new Word("journal.name_set_aside", "{0} (set aside)", "{0}: a skin's name, the game no longer having its pictures");
        internal static readonly Word MissingNote = new Word("journal.missing_note", "The mod or pack that adds this mount isn't installed. Your choice is kept, and the donkey comes instead until it is back.");
        internal static readonly Word GoneNote = new Word("journal.gone_note", "Not installed: the mod or pack that had this mount is gone. Your choice is kept; the donkey comes instead.");
        internal static readonly Word AsideNote = new Word("journal.aside_note", "Set aside: the game no longer has the pictures this mount's skin needs (the log names them). Your choice is kept; the donkey comes instead.");
        internal static readonly Word NoSkinNote = new Word("journal.no_skin_note", "It has no skin to wear. Your choice is kept; the donkey comes instead.");
        internal static readonly Word WaitingSkins = new Word("journal.waiting_skins", "Skins for this mount are installed ({0}), but not the mount itself: install the mod that adds it to ride it.", "a mount whose skins are installed but not the mount; {0}: the skins' names");
        internal static readonly Word DonkeyName = new Word("mount.donkey.name", "Donkey");
        internal static readonly Word DonkeyDescription = new Word("mount.donkey.description", "The graveyard's most patient worker. It has carried more corpses than anyone in the village, and has opinions on each.");

        // the colours window
        internal static readonly Word ColoursTitle = new Word("colours.title", "Colours: {0}", "the colours window's title; {0}: the mount's name");
        internal static readonly Word ColoursTitleSkin = new Word("colours.title_skin", "{0} - {1}", "the colours window's title with a skin; {0}: the title above, {1}: the skin's name");
        // a skin's own tones (MountSettingsPage.ToneRows): the choices, in ToneColor's order, and the custom rows' labels
        internal static readonly Word ToneGame = new Word("colours.tone_game", "Game default", "a colour for a part of a skin (one of its own tones): the skin as drawn");
        internal static readonly Word ColourOriginal = new Word("colours.original", "Original", "the colours window: a part's colour as the skin draws it (the first square of its colours, and the colour's name while it is chosen); the player's pick undone");
        // The palette's 24 colours by name, in PaletteColor's order (the colours window's part fields, 2026-11-10: the user's
        // pick, direction E, names every colour), and the plain word over a part's lightness slider.
        internal static readonly Word PaletteChestnut = new Word("colours.palette_chestnut", "Chestnut", "a colour of the colours window's palette (8B4A2B): its name in the field of the part painted with it");
        internal static readonly Word PaletteSand = new Word("colours.palette_sand", "Sand", "a colour of the colours window's palette (C8A26B): its name in the field of the part painted with it");
        internal static readonly Word PaletteCream = new Word("colours.palette_cream", "Cream", "a colour of the colours window's palette (E6D8B4): its name in the field of the part painted with it");
        internal static readonly Word PaletteSnow = new Word("colours.palette_snow", "Snow", "a colour of the colours window's palette (E8E6E0): its name in the field of the part painted with it");
        internal static readonly Word PaletteAsh = new Word("colours.palette_ash", "Ash", "a colour of the colours window's palette (8E8B85): its name in the field of the part painted with it");
        internal static readonly Word PaletteSlate = new Word("colours.palette_slate", "Slate", "a colour of the colours window's palette (5A6670): its name in the field of the part painted with it");
        internal static readonly Word PaletteCharcoal = new Word("colours.palette_charcoal", "Charcoal", "a colour of the colours window's palette (3A3A3A): its name in the field of the part painted with it");
        internal static readonly Word PaletteBlack = new Word("colours.palette_black", "Black", "a colour of the colours window's palette (1E1E1E): its name in the field of the part painted with it");
        internal static readonly Word PaletteRust = new Word("colours.palette_rust", "Rust", "a colour of the colours window's palette (A0461E): its name in the field of the part painted with it");
        internal static readonly Word PaletteBrick = new Word("colours.palette_brick", "Brick", "a colour of the colours window's palette (9C4A3A): its name in the field of the part painted with it");
        internal static readonly Word PaletteWine = new Word("colours.palette_wine", "Wine", "a colour of the colours window's palette (7A2232): its name in the field of the part painted with it");
        internal static readonly Word PaletteCoral = new Word("colours.palette_coral", "Coral", "a colour of the colours window's palette (D9765F): its name in the field of the part painted with it");
        internal static readonly Word PaletteRose = new Word("colours.palette_rose", "Rose", "a colour of the colours window's palette (C77B8F): its name in the field of the part painted with it");
        internal static readonly Word PalettePlum = new Word("colours.palette_plum", "Plum", "a colour of the colours window's palette (6B3A5E): its name in the field of the part painted with it");
        internal static readonly Word PaletteLavender = new Word("colours.palette_lavender", "Lavender", "a colour of the colours window's palette (9A86C2): its name in the field of the part painted with it");
        internal static readonly Word PaletteNavy = new Word("colours.palette_navy", "Navy", "a colour of the colours window's palette (2B3F6B): its name in the field of the part painted with it");
        internal static readonly Word PaletteSky = new Word("colours.palette_sky", "Sky", "a colour of the colours window's palette (6FA8DC): its name in the field of the part painted with it");
        internal static readonly Word PaletteTeal = new Word("colours.palette_teal", "Teal", "a colour of the colours window's palette (2F7F7A): its name in the field of the part painted with it");
        internal static readonly Word PaletteMint = new Word("colours.palette_mint", "Mint", "a colour of the colours window's palette (8FC9A3): its name in the field of the part painted with it");
        internal static readonly Word PaletteMoss = new Word("colours.palette_moss", "Moss", "a colour of the colours window's palette (5E6B34): its name in the field of the part painted with it");
        internal static readonly Word PaletteForest = new Word("colours.palette_forest", "Forest", "a colour of the colours window's palette (2F5233): its name in the field of the part painted with it");
        internal static readonly Word PaletteOlive = new Word("colours.palette_olive", "Olive", "a colour of the colours window's palette (7A7A3A): its name in the field of the part painted with it");
        internal static readonly Word PaletteMustard = new Word("colours.palette_mustard", "Mustard", "a colour of the colours window's palette (C4A23A): its name in the field of the part painted with it");
        internal static readonly Word PaletteGold = new Word("colours.palette_gold", "Gold", "a colour of the colours window's palette (C99A2E): its name in the field of the part painted with it");
        internal static readonly Word[] PaletteNames = { PaletteChestnut, PaletteSand, PaletteCream, PaletteSnow, PaletteAsh, PaletteSlate, PaletteCharcoal, PaletteBlack, PaletteRust, PaletteBrick, PaletteWine, PaletteCoral, PaletteRose, PalettePlum, PaletteLavender, PaletteNavy, PaletteSky, PaletteTeal, PaletteMint, PaletteMoss, PaletteForest, PaletteOlive, PaletteMustard, PaletteGold };
        internal static readonly Word ColourLightness = new Word("colours.lightness", "Lightness", "the colours window: the slider that makes a part's colour lighter or darker (greyed while the part is Original)");
        internal static readonly Word ToneRed = new Word("colours.tone_red", "Red", "a colour for a part of a skin");
        internal static readonly Word ToneGold = new Word("colours.tone_gold", "Gold", "a colour for a part of a skin");
        internal static readonly Word ToneGreen = new Word("colours.tone_green", "Green", "a colour for a part of a skin");
        internal static readonly Word ToneBlue = new Word("colours.tone_blue", "Blue", "a colour for a part of a skin");
        internal static readonly Word TonePurple = new Word("colours.tone_purple", "Purple", "a colour for a part of a skin");
        internal static readonly Word TonePink = new Word("colours.tone_pink", "Pink", "a colour for a part of a skin");
        internal static readonly Word ToneBrown = new Word("colours.tone_brown", "Brown", "a colour for a part of a skin");
        internal static readonly Word ToneBlack = new Word("colours.tone_black", "Black", "a colour for a part of a skin");
        internal static readonly Word ToneWhite = new Word("colours.tone_white", "White", "a colour for a part of a skin");
        internal static readonly Word ToneCustomChoice = new Word("colours.tone_custom", "Custom", "a colour for a part of a skin: the player's own, picked in the row under it");
        internal static readonly Word ToneCustom = new Word("colours.tone_custom_colour", "{0}: custom colour", "the row to pick one's own colour for a part of a skin; {0}: the part's name (the skin's)");
        internal static readonly Word ToneLightness = new Word("colours.tone_lightness", "{0}: lightness", "the row under it that makes that colour lighter or darker; {0}: the part's name");
        // a tone that follows a mount region (skin.cfg follows =): its first choice, in place of Game default (a tone's
        // choices, in ToneColor's order: SkinTones.Choices)
        // The mount regions' parts as the donkey's own look names them (SkinRegions; docs/second-tone.md 8): the colours
        // window's rows on the donkey and on a skin worn over it.
        internal static readonly Word RegionCoat = new Word("colours.region_coat", "Coat", "a part of the mount the player colours: the donkey's coat (its row's label in the colours window)");
        internal static readonly Word RegionDonkeyDark = new Word("colours.region_donkey_dark", "Legs, mane and ears", "a part of the donkey the player colours: its dark lower legs, mane, ears and tail tuft (its outline is a part of its own)");
        internal static readonly Word RegionDonkeyMuzzle = new Word("colours.region_donkey_muzzle", "Muzzle", "a part of the donkey the player colours: its pale muzzle (the nose)");
        internal static readonly Word RegionEyes = new Word("colours.region_eyes", "Eyes", "a part of the mount the player colours: its eyes");
        internal static readonly Word RegionHarness = new Word("colours.region_harness", "Harness", "a part of the mount the player colours: the donkey's harness (its straps and collar)");
        internal static readonly Word RegionOutline = new Word("colours.region_outline", "Outline", "a part of the donkey the player colours: the dark line drawn round it, its border (and the drawing's inner lines)");

        // the speeds (Catalog.SpeedBands' words, shown; mount.cfg's speed = is read in English, Catalog.ParseBand)
        internal static readonly Word SpeedVerySlow = new Word("speed.very_slow", "Very slow", "a mount's speed in the journal's stats");
        internal static readonly Word SpeedSlow = new Word("speed.slow", "Slow");
        internal static readonly Word SpeedNormal = new Word("speed.normal", "Normal");
        internal static readonly Word SpeedFast = new Word("speed.fast", "Fast");
        internal static readonly Word SpeedVeryFast = new Word("speed.very_fast", "Very fast");

        // the prompts on foot, after the game's key icon (InstanceControls, CartChest)
        internal static readonly Word PromptRide = new Word("prompt.ride", "Ride", "on foot by a mount: the key's prompt");
        internal static readonly Word PromptRideNamed = new Word("prompt.ride_named", "Ride {0}", "on foot by a mount the player named; {0}: the name they gave it");
        internal static readonly Word PromptHitch = new Word("prompt.hitch", "Hitch the cart", "riding, by a cart standing on its own");
        internal static readonly Word PromptLoad = new Word("prompt.load", "Load on cart", "on foot by a cart, carrying something");
        internal static readonly Word PromptCartFull = new Word("prompt.cart_full", "Cart is full", "on foot by a full cart, carrying something");
        internal static readonly Word PromptTake = new Word("prompt.take", "Take from cart", "on foot by a cart with something on it");
        internal static readonly Word PromptSetDown = new Word("prompt.set_down", "Set down", "the chest riding on the cart");
        internal static readonly Word PromptLoadChest = new Word("prompt.load_chest", "Load onto the cart", "riding with the cart, at a storage chest");

        // the crossroads notification (Corpses: shown in place of the game's "New Body" while the player's
        // CorpsesNotification is still this English; a text of their own as they wrote it)
        internal static readonly Word NoticeCrossroads = new Word("notice.crossroads", "The donkey left a body at the crossroads", "the notification when the game's donkey leaves a body at the crossroads");

        // ---- the keeper's bubbles: what Mount.Say shows. The code uses each one's English as its reason (MountResult.Reason
        // stays English); the bubble finds it here by that English (Shown).

        // riding
        internal static readonly Word SayNotInGame = new Word("say.not_in_game", "Not in a game.", "the keeper's thought bubble, as every say. line");
        internal static readonly Word SayStillLoading = new Word("say.still_loading", "Not yet: the game is still loading.");
        internal static readonly Word SayAlreadyMounted = new Word("say.already_mounted", "Already mounted.");
        internal static readonly Word SayNowhereToBeFound = new Word("say.nowhere_to_be_found", "Your mount is nowhere to be found.");
        internal static readonly Word SayNotInCoopGame = new Word("say.not_in_coop_game", "Not in a co-op game.");
        internal static readonly Word SayNotRightNow = new Word("say.not_right_now", "Not right now.");
        internal static readonly Word SayNotIndoors = new Word("say.not_indoors", "Not indoors.");
        internal static readonly Word SayBusy = new Word("say.busy", "I'm busy.");
        internal static readonly Word SayNotWhileBuilding = new Word("say.not_while_building", "Not while building.");
        internal static readonly Word SayNoSuchAnimal = new Word("say.no_such_animal", "There is no such animal ({0}).", "{0}: the game's id of the animal a setting names");
        internal static readonly Word SayCantRideA = new Word("say.cant_ride_a", "Can't ride a {0}: {1}", "{0}: the game's id of the animal a setting names; {1}: one of the three sentences below");
        internal static readonly Word SaySideTownQuality = new Word("say.side_town_quality", "it would change the town quality in the save.", "after \"Can't ride a ...:\"");
        internal static readonly Word SaySideNpcGroup = new Word("say.side_npc_group", "it belongs to an NPC group the save keeps track of.", "after \"Can't ride a ...:\"");
        internal static readonly Word SaySideStartItems = new Word("say.side_start_items", "it comes with items, which the cart would create out of nothing.", "after \"Can't ride a ...:\"");
        internal static readonly Word SayCartWithoutMount = new Word("say.cart_without_mount", "That is a cart without a mount.");
        internal static readonly Word SayTooSmallToRide = new Word("say.too_small_to_ride", "This one is too small to ride.");
        internal static readonly Word SayNotHereNow = new Word("say.not_here_now", "It isn't here right now.");
        internal static readonly Word SayItIsGone = new Word("say.it_is_gone", "It is gone.");

        // the cart
        internal static readonly Word SayNotCartInFight = new Word("say.not_cart_in_fight", "Not the cart in a fight.");
        internal static readonly Word SayNotCartIndoors = new Word("say.not_cart_indoors", "Not the cart indoors.");
        internal static readonly Word SayCantPullTheCart = new Word("say.cant_pull_the_cart", "{0} can't pull the cart.", "{0}: the mount's name (the player's name for it, else its kind's)");
        internal static readonly Word SayTooMuchOnCart = new Word("say.too_much_on_cart", "Too much on the cart ({0} of at most {1}). Take some off first.", "a mount that pulls fewer things than are on the cart; {0}: how many are on it, {1}: how many it pulls");
        internal static readonly Word SayNoCartHereRideUp = new Word("say.no_cart_here_ride_up", "This {0} has no cart here; ride it up to one.", "{0}: what the mount is, in lower case (donkey, crab); a translation may leave it out", mayDropValues: true);
        internal static readonly Word SayAlreadyPullsCart = new Word("say.already_pulls_cart", "This {0} already pulls a cart.", "{0}: what the mount is, in lower case (donkey, crab); a translation may leave it out", mayDropValues: true);
        internal static readonly Word SayCantPullACart = new Word("say.cant_pull_a_cart", "This one can't pull a cart.");
        internal static readonly Word SayNotAFreeCart = new Word("say.not_a_free_cart", "That is not a free cart.");
        internal static readonly Word SayCartElsewhere = new Word("say.cart_elsewhere", "The cart is somewhere else.");
        internal static readonly Word SaySceneNotLoaded = new Word("say.scene_not_loaded", "Not while the scene is not loaded.");
        internal static readonly Word SayCantGetToCart = new Word("say.cant_get_to_cart", "I can't get to that cart from here.");
        internal static readonly Word SayNoCartToLeave = new Word("say.no_cart_to_leave", "No cart to leave.");
        internal static readonly Word SayCartCantStand = new Word("say.cart_cant_stand", "The cart can't stand here.");
        internal static readonly Word SayCartNotLeftWhereStands = new Word("say.cart_not_left_where_stands", "The cart could not be left where it stands.");
        internal static readonly Word SayCartNotLeftHere = new Word("say.cart_not_left_here", "The cart could not be left here.");
        internal static readonly Word SayNoCartHere = new Word("say.no_cart_here", "No cart here.");
        internal static readonly Word SayHasNoCart = new Word("say.has_no_cart", "It has no cart.");

        // the cargo
        internal static readonly Word SayCartNotHere = new Word("say.cart_not_here", "The cart is not here.");
        internal static readonly Word SayNothingInHand = new Word("say.nothing_in_hand", "Nothing in hand.");
        internal static readonly Word SayCartTakesNothing = new Word("say.cart_takes_nothing", "The cart takes nothing now.");
        internal static readonly Word SayCartCantTake = new Word("say.cart_cant_take", "The cart can't take that.");
        internal static readonly Word SayCartFull = new Word("say.cart_full", "The cart is full.");
        internal static readonly Word SayNoKeeper = new Word("say.no_keeper", "No keeper.");
        internal static readonly Word SayCartGivesNothing = new Word("say.cart_gives_nothing", "The cart gives nothing now.");
        internal static readonly Word SayCartEmpty = new Word("say.cart_empty", "The cart is empty.");
        internal static readonly Word SayHandsFull = new Word("say.hands_full", "My hands are full.");
        internal static readonly Word SayWontComeOff = new Word("say.wont_come_off", "It won't come off the cart.");
        internal static readonly Word SayNotWhileRiding = new Word("say.not_while_riding", "Not while riding.");
        internal static readonly Word SayRiddenPickups = new Word("say.ridden_pickups", "Ridden: pickups go onto the cart already.");

        // the chest on the cart
        internal static readonly Word SayChestOff = new Word("say.chest_off", "The chest on the cart is off for now (see the log).");
        internal static readonly Word SayChestsDontRide = new Word("say.chests_dont_ride", "Chests don't ride on the cart.");
        internal static readonly Word SayNeedsTheCart = new Word("say.needs_the_cart", "That needs the cart.");
        internal static readonly Word SayOnlyPlainChest = new Word("say.only_plain_chest", "Only a plain storage chest goes on the cart.");
        internal static readonly Word SayChestAboard = new Word("say.chest_aboard", "There's a chest on the cart already.");
        internal static readonly Word SayCargoDownFirst = new Word("say.cargo_down_first", "Set the cargo down first.");
        internal static readonly Word SayCantBeMoved = new Word("say.cant_be_moved", "This can't be moved.");
        internal static readonly Word SayNotInCoop = new Word("say.not_in_coop", "Not in co-op.");
        internal static readonly Word SayMarkedForTakingDown = new Word("say.marked_for_taking_down", "It's marked for taking down.");
        internal static readonly Word SayItsBusy = new Word("say.its_busy", "It's busy.");
        internal static readonly Word SayAttachedToWorkbench = new Word("say.attached_to_workbench", "It's attached to a workbench.");
        internal static readonly Word SaySomeoneWorksAtIt = new Word("say.someone_works_at_it", "Someone works at it.");
        internal static readonly Word SaySomeoneUsingIt = new Word("say.someone_using_it", "Someone is using it right now.");
        internal static readonly Word SayCloseBuildingWindow = new Word("say.close_building_window", "Close the building window first.");
        internal static readonly Word SayZombieUsingChest = new Word("say.zombie_using_chest", "A zombie is using this chest.");
        internal static readonly Word SayDidntWork = new Word("say.didnt_work", "That didn't work.");
        internal static readonly Word SayNoRoomHere = new Word("say.no_room_here", "There's no room for it here.");
        internal static readonly Word SayCantReachSpot = new Word("say.cant_reach_spot", "I can't reach that spot.");
        internal static readonly Word SayOutsideBuildingArea = new Word("say.outside_building_area", "That spot is outside any building area.");
        internal static readonly Word SayChestStays = new Word("say.chest_stays", "The chest stays by the cart.", "a door to another place while the chest rides on the cart: it is set down by the cart");

        // flying
        internal static readonly Word SayCantFlyInHere = new Word("say.cant_fly_in_here", "Can't fly in here.");
        internal static readonly Word SayCantFlyNow = new Word("say.cant_fly_now", "Can't fly now.");
        internal static readonly Word SayNowhereToLand = new Word("say.nowhere_to_land", "Nowhere to land here.");
        internal static readonly Word SayLandFirst = new Word("say.land_first", "Land first.");

        // back to a safe spot (the Return key, or after a fall out of the world)
        internal static readonly Word SayBackOnGround = new Word("say.back_on_ground", "Back on solid ground.");
        internal static readonly Word SayKeeperNotThere = new Word("say.keeper_not_there", "The keeper's body isn't there.");
        internal static readonly Word SayNotNowDoor = new Word("say.not_now_door", "Not now: a door, a scene or a cutscene has the keeper.");
        internal static readonly Word SayNoSafeSpotYet = new Word("say.no_safe_spot_yet", "No safe spot in this scene yet (stand on the ground a moment).");
        internal static readonly Word SayNoSafeSpotHolds = new Word("say.no_safe_spot_holds", "No safe spot of this scene still holds.");
        internal static readonly Word SayNoSafeSpotAnyMore = new Word("say.no_safe_spot_any_more", "No safe spot of this scene holds any more.");

        // A speed word (Catalog.SpeedWord's English) in the player's language; another word as it is.
        internal static string Speed(string english)
        {
            foreach (Word w in new[] { SpeedVerySlow, SpeedSlow, SpeedNormal, SpeedFast, SpeedVeryFast })
            {
                if (w.English == english) return w.Text;
            }
            return english;
        }

        // ---- the bubble's lookup ----

        // The sentences templates made a moment ago (Word.Make), newest last; a reason is said in the same frame or the next.
        private static readonly (string English, Word Word, object[] Args)[] made = new (string, Word, object[])[8];
        private static int madeNext;
        private static Dictionary<string, Word> sayByEnglish;

        internal static void Remember(string english, Word w, object[] args)
        {
            made[madeNext] = (english, w, args);
            madeNext = (madeNext + 1) % made.Length;
        }

        // What the keeper's bubble shows for a sentence (Mount.Say): one of the API's in the player's language, found by
        // its English (made by a template a moment ago, the newest first; else a fixed sentence of the say. words);
        // another mod's sentence as it is.
        internal static string Shown(string english)
        {
            if (string.IsNullOrEmpty(english)) return english;
            for (int n = 1; n <= made.Length; n++)
            {
                var m = made[(madeNext - n + made.Length) % made.Length];
                if (m.Word != null && string.Equals(m.English, english, StringComparison.Ordinal)) return m.Word.Shown(m.Args);
            }
            if (sayByEnglish == null)
            {
                var map = new Dictionary<string, Word>(StringComparer.Ordinal);
                foreach (Word w in All)
                {
                    if (w.Key.StartsWith("say.", StringComparison.Ordinal) && w.English.IndexOf('{') < 0) map[w.English] = w;
                }
                sayByEnglish = map;
            }
            return sayByEnglish.TryGetValue(english, out Word found) ? found.Text : english;
        }

        // A template's values: a Term (a word, a pack's name) as its English or its translation, the rest as they are.
        internal static object[] Values(object[] args, bool shown) => Words.Values(args, shown);

        private static List<Word> all;

        // Every word, in the order above (the fields' order in the class), the window's own among them (the same objects:
        // the English file lists each key once, Texts.EnglishOf).
        internal static List<Word> All
        {
            get
            {
                if (all != null) return all;
                var list = new List<Word>();
                foreach (FieldInfo f in typeof(UiWords).GetFields(BindingFlags.Static | BindingFlags.NonPublic))
                {
                    if (f.FieldType == typeof(Word) && f.GetValue(null) is Word w && !list.Contains(w)) list.Add(w);
                }
                all = list;
                return all;
            }
        }
    }
}
