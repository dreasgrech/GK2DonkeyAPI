using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using GK2DonkeyAPI.Api;
using LazyBearTechnology;
using UnityEngine;

namespace GK2ShowcaseDirector
{
    // The shots: everything around a screenshot or a take, so that a shot is one key and a bad take costs seconds
    // (docs/showcase-video.md "Automation", docs/nexus/README.md for the pictures the page needs). Dev-only, standalone:
    // the public Donkey API and the game's own calls, nothing of the API's internals.
    //   HUD: LazyUI.Get<HUD>().SetDisableState(HudStateType.AnimationTestingManager, false) hides the whole HUD (the
    //     widget's Hide); true shows it again. That flag is the one no game code sets. The game's
    //     ReleaseTransientDisableStates re-enables every flag but MainMenu (scene changes), so the live state is read
    //     from the widget's game object, never from a remembered bool. Prompts over the world ("[E] Ride Bella") and
    //     thought bubbles are other widgets and stay.
    //   Spots: the keeper's scene and position, saved in the cfg; "Go" is PlayerController.SetPosition, on foot and in
    //     the same scene only (the ride's follow was never made for a teleported rider, so a ride is ended first).
    //   Donkey and cart: Saddle Up's companion (Mounts.Companion with its id), brought with ComeToKeeper(forRide: false)
    //     when it is elsewhere, then put beside the keeper with MoveTo (ComeToKeeper alone puts it where a ride would,
    //     under the keeper: seen 2026-10-14, the donkey stood on top of him). Its looks are set through the companion's properties for the colour variants and put
    //     back afterwards: a change in the Mods menu during a variants run would fight it.
    //   Loading: the real loading paths. Driving the cart: PlayerData.AddOverheadItem, which the API reroutes onto the
    //     cart (the game's own pickup sink, HeadReroute), and PlayerData.RemoveOverheadItem with a stub carrying the
    //     item's GUID, which the API takes off the cart (the game's own take-from-hands): the keeper stays in the
    //     saddle and the camera stands still (asked 2026-10-14: a series must not get him off). On foot at a standing
    //     cart: TryAddOverheadItemNoReplace into the hands and MountHandle.LoadFromHead, one item per frame. Riding the
    //     donkey without the cart: refused with a hint (hitch it, or get off). Items the director makes (ItemDef by id; a
    //     body from the delivery script's definition body_0_1, as the API's test loads do) are listed with their GUID in
    //     BepInEx/GK2-director-items.txt so "Remove what I made" finds them after a restart: in the hands and among the
    //     scenes' drops. A cart of the companion's whose whole load is the director's is unloaded first (SetDownCargo,
    //     the API's own set-down at the cart); a cart with anything else aboard is left to the user. Nothing else is
    //     ever removed. Zombies
    //     aren't made: that shot is set up by hand.
    //   Cargo series (a GIF of loads): the keeper stands still (the camera follows him, so the frame is fixed) and the
    //     cart stands still; one crop rectangle is computed at the start from a world box around the cart (cfg
    //     SeriesBox*) through the game's world camera, and every frame is the screen grabbed at the end of the frame
    //     (ScreenCapture.CaptureScreenshotAsTexture) and cut to that same rectangle, so all frames are the same size
    //     and place. Per configuration (cfg CargoSeries: loads separated by |, ids by ,): the cart cleared (a load
    //     that is the director's through and through is set down and taken back), the load put on, SeriesSettleFrames
    //     waited, the frame written as NN.png into a new folder with a make-gif.sh (ffmpeg, palette, no dither:
    //     pixel art). The HUD and the window are off for the whole series.
    //   Look series (a GIF of colours): the same frame machinery around the donkey (with its cart hitched, or alone),
    //     one frame per preset of each customisable part (coat, harness, cart paint, wheels, hubs, lantern light),
    //     each part walked through its presets against the base look and put back before the next part; cfg LookSeries
    //     replaces the walk with explicit frames (Coat=Brown,Paint=Blue | ...); "all parts at once" sets every part per
    //     frame at random. The same crop box as the cart series. The look is put back at the end.
    //   The API's dev panel (F11) and its zone outlines (drawn only while the panel shows) are hidden for every capture
    //     and series through Plugin.TuningWindowHidden (internal: reflection, dev tool to dev tool) and put back after.
    //   Capture: ScreenCapture.CaptureScreenshot(path, superSize) at the end of the frame after the director's window
    //     is withdrawn (an IMGUI window is in the picture) and the HUD hidden if asked; the HUD comes back once the file
    //     exists. SuperSize 1 by default: the game's post-processing (CameraFilterPack) may not survive a supersized
    //     capture; 2 is offered and the written size is logged, so a black or shifted file shows at once.
    internal sealed class Shots
    {
        private const string SaddleUpId = "dreas.gk2.saddleup";
        private const int SpotCount = 8;
        private const float DonkeyBeside = 1.6f, CartBeside = 2.6f; // world units from the keeper, sideways
        private static readonly CoatColor[] CoatVariants = { CoatColor.Brown, CoatColor.Dark, CoatColor.Cream, CoatColor.Black };
        private static readonly CartPaint[] PaintVariants = { CartPaint.DarkWood, CartPaint.Red, CartPaint.Green, CartPaint.Blue };
        private static readonly (string Label, string[] Ids)[] Loads =
        {
            ("Six logs", new[] { "wood", "wood", "wood", "wood", "wood", "wood" }),
            ("Mixed six", new[] { "wood", "wood", "stone", "wood", "box_flour", "box_vine" }),
            ("Corpse + logs", new[] { "body_corpse", "wood", "wood", "wood" }),
            ("Two logs", new[] { "wood", "wood" }),
        };
        // Random loads: N different items, drawn from the kinds the cart's layout is tuned for (no zombies: those need
        // the zombie system; no chest: that is a cart of its own).
        private static readonly string[] RandomPool =
        {
            "wood", "stone", "box_flour", "box_vine", "box_tools_steel", "iron_ore_2h", "box_beer", "box_appliances_1", "box_vegetables", "body_corpse",
        };
        private readonly System.Random rng = new System.Random();

        private readonly MonoBehaviour host;
        private readonly ManualLogSource log;
        private readonly ConfigEntry<KeyboardShortcut> captureKey;
        private readonly ConfigEntry<int> superSize;
        private readonly ConfigEntry<string> folder;
        private readonly ConfigEntry<bool> hideHudForCapture;
        private readonly ConfigEntry<string> cargoSeries;
        private readonly ConfigEntry<float> seriesBoxX, seriesBoxY, seriesBoxZ;
        private readonly ConfigEntry<int> seriesMarginPx, seriesSettleFrames;
        private readonly ConfigEntry<float> seriesFrameSeconds;
        private readonly ConfigEntry<int> sameCountFrames;
        private readonly ConfigEntry<int> holdStillBelow;
        private Color32[] prevFrame; // the series' previous frame, for HoldStillBelow
        private readonly ConfigEntry<string> lookSeries;
        private readonly ConfigEntry<int> allPartsFrames;
        private readonly ConfigEntry<string>[] spots = new ConfigEntry<string>[SpotCount];

        // Items the director made: GUID and the scene they were made in (for the drops), kept in a file across restarts.
        private readonly List<(Guid Id, string Scene)> made = new List<(Guid, string)>();
        private bool madeLoaded;

        private Coroutine running;
        private string status = "";
        private int shotCounter;

        internal bool Capturing { get; private set; }
        internal KeyboardShortcut CaptureKey => captureKey.Value;

        internal Shots(MonoBehaviour host, ConfigFile cfg, ManualLogSource log)
        {
            this.host = host;
            this.log = log;
            captureKey = cfg.Bind("Shots", "CaptureKey", new KeyboardShortcut(KeyCode.F10), "Takes a screenshot (the director's window withdrawn; the HUD hidden if HideHudForCapture).");
            superSize = cfg.Bind("Shots", "SuperSize", 1, new ConfigDescription("Screenshot size as a multiple of the screen (1 or 2). 2 may not survive the game's post-processing: check the written file.", new AcceptableValueRange<int>(1, 2)));
            string pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            folder = cfg.Bind("Shots", "Folder", Path.Combine(string.IsNullOrEmpty(pictures) ? Paths.GameRootPath : pictures, "GK2 Saddle Up shots"), "Where the screenshots go.");
            hideHudForCapture = cfg.Bind("Shots", "HideHudForCapture", true, "Hide the HUD for the capture and show it again after.");
            cargoSeries = cfg.Bind("Series", "CargoSeries",
                "wood | wood,wood | wood,stone | body_corpse,wood | box_flour,box_vine,wood | body_corpse,wood,wood,stone | box_flour,box_beer,wood,wood,stone | wood,wood,wood,stone,box_flour,box_vine",
                "The loads of the cargo series, one frame each: loads separated by |, item ids by , (wood, stone, box_flour, box_vine, box_tools_steel, iron_ore_2h, box_beer, box_appliances_1, box_vegetables, body_corpse).");
            seriesBoxX = cfg.Bind("Series", "BoxX", 1.7f, "Half width (world units, sideways) of the box around the cart (or the donkey, look series) that becomes the crop of every series.");
            seriesBoxY = cfg.Bind("Series", "BoxY", 1.9f, "Height (world units) of the box above the cart's ground point.");
            seriesBoxZ = cfg.Bind("Series", "BoxZ", 0.7f, "Half depth (world units, up/down the screen) of the box.");
            seriesMarginPx = cfg.Bind("Series", "MarginPx", 24, "Screen pixels added around the box.");
            seriesSettleFrames = cfg.Bind("Series", "SettleFrames", 12, "Frames waited after a load before its frame is grabbed.");
            seriesFrameSeconds = cfg.Bind("Series", "FrameSeconds", 0.1f, "How long each frame shows in the GIF, seconds (a list of loads or colours flicks through at 0.1; make-gif.sh takes a scale and the seconds as arguments too).");
            holdStillBelow = cfg.Bind("Series", "HoldStillBelow", 16, new ConfigDescription(
                "Series frames: a pixel that differs from the previous frame by less than this (0-255, per channel) keeps the previous frame's value, so the lighting flicker of a still background is not re-encoded in the GIF (measured 2026-10-14: 16 halves a cargo GIF and cuts a look GIF to a fifth, with no visible change; 0 = off).",
                new AcceptableValueRange<int>(0, 64)));
            sameCountFrames = cfg.Bind("Series", "SameCountFrames", 4, new ConfigDescription("How many different random loads the 'same count' series takes of one count.", new AcceptableValueRange<int>(2, 12)));
            lookSeries = cfg.Bind("Series", "LookSeries", "",
                "Empty: every preset of every part in turn (Coat, Harness, Paint, Wheels, Hubs, LanternLight), the rest at your look. Else explicit frames: Part=Preset pairs separated by , and frames by | (e.g. Coat=Brown,Paint=Blue | Coat=Dark,Harness=Red).");
            allPartsFrames = cfg.Bind("Series", "AllPartsFrames", 16, new ConfigDescription("How many frames the 'all parts at once' look series takes: every part a random colour in each frame (the API's 24-colour palette at five lightnesses through the part's Custom setting, or a preset), no two frames alike.", new AcceptableValueRange<int>(2, 60)));
            for (int i = 0; i < SpotCount; i++)
            {
                spots[i] = cfg.Bind("Spots", "Spot" + (i + 1), "", "scene|x|y|z, saved from the window.");
            }
        }

        // Every frame in a game (Plugin.Update).
        internal void Tick()
        {
            if (running == null && captureKey.Value.IsDown())
            {
                Capture("shot");
            }
        }

        // ------------------------------------------------------------------ window

        internal void Draw()
        {
            GUIStyle rich = Rich;
            GUILayout.Label("<b>Shots</b>" + (running != null ? "  (busy)" : ""), rich);
            GUI.enabled = running == null;

            // Capture and HUD
            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"Capture ({captureKey.Value})"))
            {
                Capture("shot");
            }
            HUD hud = Hud();
            bool hudVisible = hud != null && hud.gameObject.activeSelf;
            if (GUILayout.Button(hud == null ? "HUD: not found" : hudVisible ? "Hide the HUD" : "Show the HUD") && hud != null)
            {
                SetHud(!hudVisible);
            }
            GUILayout.EndHorizontal();
            GUILayout.Label($"Size x{superSize.Value}, HUD {(hideHudForCapture.Value ? "hidden" : "kept")} for the capture, into {folder.Value}", rich);

            // Spots
            GUILayout.Label("<b>Spots</b> (the keeper's place; Go ends a ride first, same scene only)", rich);
            PlayerData pd = MainGame.PlayerData;
            for (int i = 0; i < SpotCount; i++)
            {
                GUILayout.BeginHorizontal();
                string v = spots[i].Value;
                bool has = TryParseSpot(v, out string scene, out Vector3 at);
                GUILayout.Label($"{i + 1}. {(has ? $"{scene} ({at.x:F1}, {at.y:F1}, {at.z:F1})" : "-")}", GUILayout.Width(250f));
                if (GUILayout.Button("Save here", GUILayout.Width(80f)) && pd != null)
                {
                    Vector3 p = pd.position.Value;
                    spots[i].Value = $"{pd.currentGameSceneId}|{p.x:F3}|{p.y:F3}|{p.z:F3}";
                    log.LogInfo($"[director] spot {i + 1} saved: {spots[i].Value}");
                }
                GUI.enabled = running == null && has;
                if (GUILayout.Button("Go", GUILayout.Width(40f)))
                {
                    GoTo(i + 1, scene, at);
                }
                GUI.enabled = running == null;
                GUILayout.EndHorizontal();
            }

            // Donkey and cart (Saddle Up's)
            GUILayout.Label("<b>Donkey and cart</b> (Saddle Up's own; press H once in this save first)", rich);
            MountCompanion me = Companion();
            MountHandle donkey = me?.Donkey, cart = me?.Cart;
            GUILayout.Label(donkey == null ? "No donkey yet." : $"Donkey {(donkey.IsRidden ? "ridden" : "standing")}, {(donkey.HasCart ? "with its cart" : cart != null ? "cart parked on its own" : "no cart")}, {(donkey.Cargo.Count)} aboard{(donkey.Chest != null ? ", a chest" : "")}.");
            GUI.enabled = running == null && donkey != null;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("← Donkey"))
            {
                Run(Bring(donkey, false, DonkeyBeside));
            }
            if (GUILayout.Button("Donkey →"))
            {
                Run(Bring(donkey, true, DonkeyBeside));
            }
            GUI.enabled = running == null && cart != null;
            if (GUILayout.Button("← Cart"))
            {
                Run(Bring(cart, false, CartBeside));
            }
            if (GUILayout.Button("Cart →"))
            {
                Run(Bring(cart, true, CartBeside));
            }
            GUI.enabled = running == null && Mounts.IsMounted;
            if (GUILayout.Button("Get off"))
            {
                Mounts.Dismount("director");
            }
            GUILayout.EndHorizontal();

            // Looks
            GUI.enabled = running == null && donkey != null;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Coat variants (4 shots)"))
            {
                Run(Variants(me, coat: true));
            }
            if (GUILayout.Button("Cart paint variants (4 shots)"))
            {
                Run(Variants(me, coat: false));
            }
            if (GUILayout.Button("Look series (every preset)"))
            {
                Run(LookSeries(me, donkey, LookFrames(lookSeries.Value), "look"));
            }
            if (GUILayout.Button($"All parts at once ({allPartsFrames.Value})"))
            {
                Run(LookSeries(me, donkey, AllPartsFrames(allPartsFrames.Value), "lookall"));
            }
            GUILayout.EndHorizontal();
            // The counts, set here (the director has no Mods-menu page; the cfg is its only home, and a value already
            // in the cfg never follows a new default).
            GUILayout.BeginHorizontal();
            Stepper("All-parts frames", allPartsFrames, 2, 60, 4);
            Stepper("Same-count loads", sameCountFrames, 2, 12, 1);
            GUILayout.EndHorizontal();

            // Loads (riding: the keeper gets off first; the cart may be hitched or standing on its own)
            MountHandle loadCart = CartForLoading(me);
            GUI.enabled = running == null && loadCart != null;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Load:", GUILayout.Width(40f));
            foreach ((string label, string[] ids) in Loads)
            {
                if (GUILayout.Button(label))
                {
                    Run(Load(loadCart, ids, label));
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Series:", GUILayout.Width(55f));
            if (GUILayout.Button("Cargo series (cfg list)"))
            {
                Run(Series(loadCart, ParseSeries(cargoSeries.Value), "cargo"));
            }
            if (GUILayout.Button("Random series 1..6"))
            {
                Run(Series(loadCart, RandomSeries(1), "random"));
            }
            GUILayout.EndHorizontal();
            // Several random loads per count, one up to six (the GIF walks up the counts with variety).
            GUILayout.BeginHorizontal();
            GUILayout.Label("1..6, each:", GUILayout.Width(75f));
            foreach (int k in new[] { 2, 3, 4, 5 })
            {
                if (GUILayout.Button($"{k} loads"))
                {
                    Run(Series(loadCart, RandomSeries(k), $"random{k}each"));
                }
            }
            GUILayout.EndHorizontal();
            // Several different random loads of one count.
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{sameCountFrames.Value} of:", GUILayout.Width(75f));
            for (int n = 1; n <= 6; n++)
            {
                if (GUILayout.Button(n + (n == 1 ? " item" : " items")))
                {
                    Run(Series(loadCart, RandomLoads(n, sameCountFrames.Value), $"random{n}x{sameCountFrames.Value}"));
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Random:", GUILayout.Width(55f));
            for (int n = 1; n <= 6; n++)
            {
                if (GUILayout.Button(n + (n == 1 ? " item" : " different")))
                {
                    Run(Load(loadCart, RandomLoad(n), $"Random {n}"));
                }
            }
            GUILayout.EndHorizontal();
            GUI.enabled = running == null;

            // Items on the ground
            GUILayout.BeginHorizontal();
            GUILayout.Label("Drop:", GUILayout.Width(40f));
            if (GUILayout.Button("3 logs"))
            {
                DropAtFeet("wood", 3);
            }
            if (GUILayout.Button("2 stone"))
            {
                DropAtFeet("stone", 2);
            }
            if (GUILayout.Button("a corpse"))
            {
                DropAtFeet("body_corpse", 1);
            }
            LoadMade();
            if (GUILayout.Button($"Remove what I made ({made.Count})"))
            {
                Run(RemoveMadeRoutine());
            }
            // The cart's whole load marked as the director's (for a load the director made but no longer lists: it
            // forgot loads on a ridden cart until 2026-10-14). The user decides; the items are then removable.
            GUI.enabled = running == null && CartForLoading(me) != null && CartForLoading(me).Cargo.Count > 0;
            if (GUILayout.Button("Claim the cart's load"))
            {
                ClaimCartLoad(CartForLoading(me));
            }
            GUI.enabled = running == null;
            GUILayout.EndHorizontal();
            GUI.enabled = true;
            if (status.Length > 0)
            {
                GUILayout.Label(status, rich);
            }
        }

        private static void Stepper(string label, ConfigEntry<int> entry, int min, int max, int step)
        {
            GUILayout.Label($"{label}: {entry.Value}", GUILayout.Width(150f));
            if (GUILayout.Button("-", GUILayout.Width(26f)))
            {
                entry.Value = Mathf.Max(min, entry.Value - step);
            }
            if (GUILayout.Button("+", GUILayout.Width(26f)))
            {
                entry.Value = Mathf.Min(max, entry.Value + step);
            }
        }

        // ------------------------------------------------------------------ HUD

        private static HUD Hud()
        {
            try
            {
                return LazyUI.Get<HUD>();
            }
            catch
            {
                return null;
            }
        }

        private void SetHud(bool show)
        {
            HUD hud = Hud();
            if (hud == null)
            {
                return;
            }
            hud.SetDisableState(HudStateType.AnimationTestingManager, show);
            log.LogInfo($"[director] HUD {(show ? "shown" : "hidden")}.");
        }

        // ------------------------------------------------------------------ spots

        private static bool TryParseSpot(string v, out string scene, out Vector3 at)
        {
            scene = null;
            at = Vector3.zero;
            if (string.IsNullOrEmpty(v))
            {
                return false;
            }
            string[] p = v.Split('|');
            if (p.Length != 4 || !float.TryParse(p[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x)
                || !float.TryParse(p[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y)
                || !float.TryParse(p[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float z))
            {
                return false;
            }
            scene = p[0];
            at = new Vector3(x, y, z);
            return true;
        }

        private void GoTo(int n, string scene, Vector3 at)
        {
            PlayerData pd = MainGame.PlayerData;
            if (pd == null)
            {
                return;
            }
            if (pd.currentGameSceneId != scene)
            {
                Say($"Spot {n} is in {scene}; you are in {pd.currentGameSceneId}. Walk there first.");
                return;
            }
            Run(GoRoutine(n, at));
        }

        private IEnumerator GoRoutine(int n, Vector3 at)
        {
            if (Mounts.IsMounted)
            {
                Mounts.Dismount("director");
                yield return null;
                yield return null;
            }
            PlayerController pc = MainGame.PlayerController;
            if (pc == null)
            {
                yield break;
            }
            pc.SetPosition(at);
            Say($"At spot {n}.");
            log.LogInfo($"[director] keeper to spot {n}: {at}");
        }

        // ------------------------------------------------------------------ donkey and cart

        private static MountCompanion Companion()
        {
            try
            {
                return Mounts.Companion(SaddleUpId);
            }
            catch
            {
                return null;
            }
        }

        // The cart to load: the donkey's while hitched (ridden or not: the load gets the keeper off first), else the
        // parked one.
        private static MountHandle CartForLoading(MountCompanion me)
        {
            if (me == null)
            {
                return null;
            }
            if (me.Donkey != null && me.Donkey.Exists && me.Donkey.HasCart)
            {
                return me.Donkey;
            }
            if (me.Cart != null && me.Cart.Exists)
            {
                return me.Cart;
            }
            return null;
        }

        // k random loads of each count from 1 to 6, the counts ascending; the loads of one count differ from each other.
        private List<string[]> RandomSeries(int k)
        {
            var loads = new List<string[]>();
            for (int n = 1; n <= 6; n++)
            {
                loads.AddRange(RandomLoads(n, k));
            }
            return loads;
        }

        // k different random loads of n items (as sets: the same kinds in another order count as the same load).
        private List<string[]> RandomLoads(int n, int k)
        {
            var loads = new List<string[]>();
            var seen = new HashSet<string>();
            for (int tries = 0; loads.Count < k && tries < k * 20; tries++)
            {
                string[] load = RandomLoad(n);
                var sorted = new List<string>(load);
                sorted.Sort(StringComparer.Ordinal);
                if (seen.Add(string.Join(",", sorted)))
                {
                    loads.Add(load);
                }
            }
            return loads;
        }

        // n different kinds, in a random order.
        private string[] RandomLoad(int n)
        {
            var pool = new List<string>(RandomPool);
            var pick = new List<string>();
            while (pick.Count < n && pool.Count > 0)
            {
                int i = rng.Next(pool.Count);
                pick.Add(pool[i]);
                pool.RemoveAt(i);
            }
            return pick.ToArray();
        }

        // Beside the keeper: on his right (rightSide) or left, that far away, facing him. From another scene, or not yet
        // loaded here, it is called first (ComeToKeeper) and moved once its view is ready.
        private IEnumerator Bring(MountHandle h, bool rightSide, float beside)
        {
            PlayerData pd = MainGame.PlayerData;
            if (h == null || pd == null)
            {
                yield break;
            }
            string what = h.HasDonkey ? "donkey" : "cart";
            if (h.SceneId != pd.currentGameSceneId || !h.IsReady)
            {
                MountResult come = h.ComeToKeeper(null, forRide: false, showMessage: false);
                if (!come.Ok)
                {
                    Say($"The {what} refused to come: {come.Reason}");
                    log.LogInfo($"[director] bring {what}: refused, {come.Reason}");
                    yield break;
                }
                for (int i = 0; i < 300 && !h.IsReady; i++)
                {
                    yield return null;
                }
                if (!h.IsReady)
                {
                    Say($"The {what} came but its view is not ready; try again.");
                    yield break;
                }
            }
            Vector3 at = pd.position.Value + new Vector3(rightSide ? beside : -beside, 0f, 0f);
            MountResult r = h.MoveTo(at, !rightSide); // facing the keeper
            Say(r.Ok ? $"The {what} stands {(rightSide ? "right" : "left")} of you." : $"The {what} would not move: {r.Reason}");
            log.LogInfo($"[director] {what} beside the keeper ({(rightSide ? "right" : "left")}, {beside}): {(r.Ok ? "ok" : r.Reason)}");
        }

        private IEnumerator Variants(MountCompanion me, bool coat)
        {
            CoatColor coat0 = me.Coat;
            CartPaint paint0 = me.Paint;
            int n = coat ? CoatVariants.Length : PaintVariants.Length;
            for (int i = 0; i < n; i++)
            {
                string name;
                if (coat)
                {
                    me.Coat = CoatVariants[i];
                    name = "coat_" + CoatVariants[i];
                }
                else
                {
                    me.Paint = PaintVariants[i];
                    name = "paint_" + PaintVariants[i];
                }
                yield return null;
                yield return null;
                yield return null;
                yield return CaptureRoutine(name);
            }
            me.Coat = coat0;
            me.Paint = paint0;
            Say($"{n} variants taken; the look is back to {(coat ? coat0.ToString() : paint0.ToString())}.");
        }

        // ------------------------------------------------------------------ loads and drops

        private int loadDone;
        private string loadWhy;

        // A load is the whole load: what the director put on before is taken off first (a load of the player's is never
        // touched: refused instead), so "3 different" always ends with exactly three aboard.
        private IEnumerator Load(MountHandle cart, string[] ids, string label)
        {
            if (cart == null || !cart.Exists)
            {
                Say("No cart.");
                yield break;
            }
            string no = WhyNotNow(cart);
            if (no != null)
            {
                Say(no);
                yield break;
            }
            if (cart.Cargo.Count > 0)
            {
                if (!AllMine(cart))
                {
                    Say("The cart holds a load of yours: set it down first (a load only ever replaces the director's own).");
                    yield break;
                }
                yield return ClearCart(cart);
            }
            yield return LoadItems(cart, ids);
            Say($"{label}: {loadDone} of {ids.Length} loaded{(loadWhy != null ? "; stopped at " + loadWhy : "")}.");
            log.LogInfo($"[director] load {label}: {loadDone}/{ids.Length}{(loadWhy != null ? ", stopped: " + loadWhy : "")}");
        }

        // Loading on foot is the real path (LoadFromHead refuses a ride); the donkey and cart stay where they are.
        private IEnumerator OnFootAt(MountHandle cart)
        {
            if (!Mounts.IsMounted)
            {
                yield break;
            }
            Mounts.Dismount("director");
            yield return null;
            yield return null;
            for (int i = 0; i < 180 && cart != null && cart.Exists && !cart.IsReady; i++)
            {
                yield return null;
            }
        }

        // The items onto the standing cart, one per frame; loadDone / loadWhy say how it went.
        private IEnumerator LoadItems(MountHandle cart, string[] ids)
        {
            loadDone = 0;
            loadWhy = null;
            PlayerData pd = MainGame.PlayerData;
            if (pd == null || cart == null || !cart.Exists)
            {
                loadWhy = "no cart";
                yield break;
            }
            bool driving = Driving(cart);
            if (!driving && pd.HasOverheadItem)
            {
                loadWhy = "your hands are full: set that down first";
                yield break;
            }
            foreach (string id in ids)
            {
                Item item = Make(id, out string note);
                if (item == null)
                {
                    loadWhy = $"{id}: {note}";
                    break;
                }
                if (driving)
                {
                    // The game's own pickup sink: the API puts it on the cart (HeadReroute), or, cart full, in front of
                    // the keeper, from where it is taken back here.
                    string key = item.UniqueId.ToString();
                    Remember(item, pd.currentGameSceneId);
                    pd.AddOverheadItem(item);
                    bool aboard = false;
                    foreach (CargoItem c in cart.Cargo)
                    {
                        if (c.UniqueId == key)
                        {
                            aboard = true;
                        }
                    }
                    if (!aboard)
                    {
                        int back = RemoveMineFromGround();
                        loadWhy = $"{id}: the cart refused it ({(back > 0 ? "taken back from the ground" : "not found on the ground either")})";
                        break;
                    }
                    loadDone++;
                    yield return null;
                    continue;
                }
                if (!pd.TryAddOverheadItemNoReplace(item))
                {
                    loadWhy = $"{id}: the hands refused it";
                    break;
                }
                Remember(item, pd.currentGameSceneId);
                MountResult r = cart.LoadFromHead(false);
                if (!r.Ok)
                {
                    // back out of the hands: nothing of the director's is left in them
                    pd.RemoveOverheadItem(item);
                    Forget(item);
                    loadWhy = $"{id}: {r.Reason}";
                    break;
                }
                loadDone++;
                yield return null;
            }
        }

        // ------------------------------------------------------------------ the cargo series

        private static List<string[]> ParseSeries(string text)
        {
            var loads = new List<string[]>();
            foreach (string part in (text ?? "").Split('|'))
            {
                var ids = new List<string>();
                foreach (string id in part.Split(','))
                {
                    string t = id.Trim();
                    if (t.Length > 0)
                    {
                        ids.Add(t);
                    }
                }
                if (ids.Count > 0)
                {
                    loads.Add(ids.ToArray());
                }
            }
            return loads;
        }

        private IEnumerator Series(MountHandle cart, List<string[]> loads, string name)
        {
            if (cart == null || loads == null || loads.Count == 0)
            {
                Say("No cart, or an empty series.");
                yield break;
            }
            string no = WhyNotNow(cart);
            if (no != null)
            {
                Say(no);
                yield break;
            }
            if (!cart.Exists || !cart.IsReady)
            {
                Say("The cart is not here.");
                yield break;
            }
            if (!AllMine(cart))
            {
                Say("The cart holds a load of yours: set it down first (the series clears the cart between frames).");
                yield break;
            }
            Camera cam = CameraSystem.Instance != null ? CameraSystem.Instance.WorldCamera : Camera.main;
            if (cam == null)
            {
                Say("No camera found.");
                yield break;
            }
            // The one crop rectangle: the box around the cart, projected now; the camera and the cart stand still from
            // here on (the keeper does not move: the window is off, and the loads are put on from where he stands).
            RectInt crop = CropRect(cam, cart.Position, seriesBoxX.Value, seriesBoxY.Value, seriesBoxZ.Value);
            prevFrame = null;
            string dir = Path.Combine(folder.Value, $"series_{name}_{DateTime.Now:yyyyMMdd_HHmmss}");
            Directory.CreateDirectory(dir);
            Capturing = true;
            HidePanel();
            HUD hud = Hud();
            bool hid = false;
            if (hud != null && hud.gameObject.activeSelf)
            {
                SetHud(false);
                hid = true;
            }
            yield return null;
            int frames = 0;
            var notes = new List<string>();
            for (int i = 0; i < loads.Count; i++)
            {
                yield return ClearCart(cart);
                yield return LoadItems(cart, loads[i]);
                if (loadWhy != null)
                {
                    notes.Add($"frame {i + 1}: {loadDone}/{loads[i].Length}, {loadWhy}");
                }
                for (int f = 0; f < Mathf.Max(1, seriesSettleFrames.Value); f++)
                {
                    yield return null;
                }
                yield return new WaitForEndOfFrame();
                string path = Path.Combine(dir, $"{i + 1:00}.png");
                string err = GrabCropped(cam, crop, path);
                if (err != null)
                {
                    notes.Add($"frame {i + 1}: {err}");
                }
                else
                {
                    frames++;
                }
                log.LogInfo($"[director] series frame {i + 1}/{loads.Count}: {string.Join("+", loads[i])} -> {(err ?? Path.GetFileName(path))}");
            }
            yield return ClearCart(cart);
            if (hid)
            {
                SetHud(true);
            }
            RestorePanel();
            Capturing = false;
            WriteGifScript(dir, crop);
            string summary = $"Series: {frames} of {loads.Count} frames, {crop.width}x{crop.height}, in {dir}{(notes.Count > 0 ? "; " + string.Join("; ", notes) : "")}.";
            Say(summary);
            log.LogInfo("[director] " + summary);
        }

        // One frame of the look series: the parts it sets.
        private sealed class LookFrame
        {
            public string Label;
            public readonly List<(string Part, string Preset)> Parts = new List<(string, string)>();
        }

        private static readonly string[] LookParts = { "Coat", "Harness", "Paint", "Wheels", "Hubs", "LanternLight" };

        private static Type PartType(string part)
        {
            switch (part)
            {
                case "Coat": return typeof(CoatColor);
                case "Harness": return typeof(HarnessColor);
                case "Paint": return typeof(CartPaint);
                case "Wheels": return typeof(WheelColor);
                case "Hubs": return typeof(HubColor);
                case "LanternLight": return typeof(LanternColor);
                default: return null;
            }
        }

        private static string GetCustom(MountCompanion me, string part)
        {
            switch (part)
            {
                case "Coat": return me.CustomCoat;
                case "Harness": return me.CustomHarness;
                case "Paint": return me.CustomPaint;
                case "Wheels": return me.CustomWheels;
                case "Hubs": return me.CustomHubs;
                case "LanternLight": return me.CustomLantern;
                default: return null;
            }
        }

        private static void SetCustom(MountCompanion me, string part, string hex)
        {
            switch (part)
            {
                case "Coat": me.CustomCoat = hex; break;
                case "Harness": me.CustomHarness = hex; break;
                case "Paint": me.CustomPaint = hex; break;
                case "Wheels": me.CustomWheels = hex; break;
                case "Hubs": me.CustomHubs = hex; break;
                case "LanternLight": me.CustomLantern = hex; break;
            }
        }

        // A frame's value for a part: a preset name, or "Custom:RRGGBB" (the part set to Custom with that colour).
        private static void ApplyPart(MountCompanion me, string part, string preset)
        {
            if (preset.StartsWith("Custom:", StringComparison.OrdinalIgnoreCase))
            {
                SetCustom(me, part, preset.Substring(7));
                SetPart(me, part, Enum.Parse(PartType(part), "Custom", true));
                return;
            }
            SetPart(me, part, Enum.Parse(PartType(part), preset, true));
        }

        private static object GetPart(MountCompanion me, string part)
        {
            switch (part)
            {
                case "Coat": return me.Coat;
                case "Harness": return me.Harness;
                case "Paint": return me.Paint;
                case "Wheels": return me.Wheels;
                case "Hubs": return me.Hubs;
                case "LanternLight": return me.LanternLight;
                default: return null;
            }
        }

        private static void SetPart(MountCompanion me, string part, object value)
        {
            switch (part)
            {
                case "Coat": me.Coat = (CoatColor)value; break;
                case "Harness": me.Harness = (HarnessColor)value; break;
                case "Paint": me.Paint = (CartPaint)value; break;
                case "Wheels": me.Wheels = (WheelColor)value; break;
                case "Hubs": me.Hubs = (HubColor)value; break;
                case "LanternLight": me.LanternLight = (LanternColor)value; break;
            }
        }

        // The frames: the cfg's explicit list, or every preset (not Custom) of every part in turn.
        private List<LookFrame> LookFrames(string cfgText)
        {
            var frames = new List<LookFrame>();
            if (!string.IsNullOrWhiteSpace(cfgText))
            {
                foreach (string part in cfgText.Split('|'))
                {
                    var f = new LookFrame();
                    foreach (string pair in part.Split(','))
                    {
                        string[] kv = pair.Split('=');
                        if (kv.Length == 2 && PartType(kv[0].Trim()) != null)
                        {
                            f.Parts.Add((kv[0].Trim(), kv[1].Trim()));
                        }
                    }
                    if (f.Parts.Count > 0)
                    {
                        f.Label = string.Join(",", f.Parts.ConvertAll(p => p.Part + "=" + p.Preset));
                        frames.Add(f);
                    }
                }
                return frames;
            }
            foreach (string part in LookParts)
            {
                foreach (object v in Enum.GetValues(PartType(part)))
                {
                    if (v.ToString() == "Custom")
                    {
                        continue;
                    }
                    var f = new LookFrame { Label = part + "=" + v };
                    f.Parts.Add((part, v.ToString()));
                    frames.Add(f);
                }
            }
            return frames;
        }

        private static readonly int[] Lightnesses = { 70, 85, 100, 115, 130 }; // of the palette's 40..160

        // n frames, every part a random colour in each: three in four from the API's palette (24 colours at five
        // lightnesses, through the part's Custom setting), else a preset (not Game, not Custom); no two frames alike.
        private List<LookFrame> AllPartsFrames(int n)
        {
            var frames = new List<LookFrame>();
            var seen = new HashSet<string>();
            var presets = new Dictionary<string, List<string>>();
            foreach (string part in LookParts)
            {
                var list = new List<string>();
                foreach (object v in Enum.GetValues(PartType(part)))
                {
                    string name = v.ToString();
                    if (name != "Custom" && name != "Game")
                    {
                        list.Add(name);
                    }
                }
                presets[part] = list;
            }
            Array palette = Enum.GetValues(typeof(PaletteColor));
            for (int tries = 0; frames.Count < n && tries < n * 20; tries++)
            {
                var f = new LookFrame();
                foreach (string part in LookParts)
                {
                    if (rng.Next(4) < 3)
                    {
                        PaletteColor c = (PaletteColor)palette.GetValue(rng.Next(palette.Length));
                        f.Parts.Add((part, "Custom:" + MountPalette.Hex(c, Lightnesses[rng.Next(Lightnesses.Length)])));
                    }
                    else
                    {
                        List<string> list = presets[part];
                        f.Parts.Add((part, list[rng.Next(list.Count)]));
                    }
                }
                f.Label = string.Join(",", f.Parts.ConvertAll(p => p.Part + "=" + p.Preset));
                if (seen.Add(f.Label))
                {
                    frames.Add(f);
                }
            }
            return frames;
        }

        private IEnumerator LookSeries(MountCompanion me, MountHandle donkey, List<LookFrame> frames, string name)
        {
            if (me == null || donkey == null || !donkey.Exists)
            {
                Say("No donkey.");
                yield break;
            }
            for (int i = 0; i < 180 && !donkey.IsReady; i++)
            {
                yield return null;
            }
            if (!donkey.IsReady)
            {
                Say("The donkey is not here.");
                yield break;
            }
            if (frames == null || frames.Count == 0)
            {
                Say("No usable frame (LookSeries in the cfg?).");
                yield break;
            }
            Camera cam = CameraSystem.Instance != null ? CameraSystem.Instance.WorldCamera : Camera.main;
            if (cam == null)
            {
                Say("No camera found.");
                yield break;
            }
            // The base look, put back after each frame and at the end.
            var original = new Dictionary<string, object>();
            var originalCustom = new Dictionary<string, string>();
            foreach (string part in LookParts)
            {
                original[part] = GetPart(me, part);
                originalCustom[part] = GetCustom(me, part);
            }
            // The same box as the cart series (one set of dimensions for every GIF; the user, 2026-10-14).
            RectInt crop = CropRect(cam, donkey.Position, seriesBoxX.Value, seriesBoxY.Value, seriesBoxZ.Value);
            prevFrame = null;
            string dir = Path.Combine(folder.Value, $"series_{name}_{DateTime.Now:yyyyMMdd_HHmmss}");
            Directory.CreateDirectory(dir);
            Capturing = true;
            HidePanel();
            HUD hud = Hud();
            bool hid = false;
            if (hud != null && hud.gameObject.activeSelf)
            {
                SetHud(false);
                hid = true;
            }
            yield return null;
            int done = 0;
            var notes = new List<string>();
            var labels = new List<string>();
            try
            {
                for (int i = 0; i < frames.Count; i++)
                {
                    LookFrame f = frames[i];
                    foreach ((string part, string preset) in f.Parts)
                    {
                        try
                        {
                            ApplyPart(me, part, preset);
                        }
                        catch (Exception)
                        {
                            notes.Add($"frame {i + 1}: no preset {preset} for {part}");
                        }
                    }
                    for (int w = 0; w < Mathf.Max(2, seriesSettleFrames.Value); w++)
                    {
                        yield return null;
                    }
                    yield return new WaitForEndOfFrame();
                    string path = Path.Combine(dir, $"{i + 1:00}.png");
                    string err = GrabCropped(cam, crop, path);
                    if (err != null)
                    {
                        notes.Add($"frame {i + 1}: {err}");
                    }
                    else
                    {
                        done++;
                    }
                    labels.Add($"{i + 1:00} {f.Label}");
                    log.LogInfo($"[director] look frame {i + 1}/{frames.Count}: {f.Label} -> {(err ?? Path.GetFileName(path))}");
                    foreach ((string part, string _) in f.Parts)
                    {
                        SetCustom(me, part, originalCustom[part]);
                        SetPart(me, part, original[part]);
                    }
                    yield return null;
                }
            }
            finally
            {
                foreach (string part in LookParts)
                {
                    SetCustom(me, part, originalCustom[part]);
                    SetPart(me, part, original[part]);
                }
                if (hid)
                {
                    SetHud(true);
                }
                RestorePanel();
                Capturing = false;
            }
            WriteGifScript(dir, crop);
            try
            {
                File.WriteAllLines(Path.Combine(dir, "frames.txt"), labels);
            }
            catch (Exception e)
            {
                log.LogWarning("[director] writing frames.txt failed: " + e.Message);
            }
            string summary = $"Look series: {done} of {frames.Count} frames, {crop.width}x{crop.height}, in {dir}{(notes.Count > 0 ? "; " + string.Join("; ", notes) : "")}; the look is back as it was.";
            Say(summary);
            log.LogInfo("[director] " + summary);
        }

        private bool AllMine(MountHandle h)
        {
            LoadMade();
            var mine = new HashSet<string>();
            foreach ((Guid id, string _) in made)
            {
                mine.Add(id.ToString());
            }
            foreach (CargoItem c in h.Cargo)
            {
                if (!mine.Contains(c.UniqueId))
                {
                    return false;
                }
            }
            return true;
        }

        // The director's load off the cart and out of the world: set down at the cart (the API's own set-down), then
        // the drops of the director's taken back. Nothing of the player's is touched (AllMine is checked by the caller).
        private IEnumerator ClearCart(MountHandle cart)
        {
            if (cart.Cargo.Count == 0 || !AllMine(cart))
            {
                yield break;
            }
            if (Driving(cart))
            {
                // The game's own take-from-hands, by GUID: the API takes the item off the cart (HeadReroute), and it is
                // gone, as when a pile takes it. A stub item carries the GUID; nothing else of it is used.
                PlayerData pd = MainGame.PlayerData;
                int n = 0;
                foreach (CargoItem c in new List<CargoItem>(cart.Cargo))
                {
                    if (!Guid.TryParse(c.UniqueId, out Guid g))
                    {
                        continue;
                    }
                    Item stub = new Item(c.Id);
                    stub.UniqueId.SetGuid(new SGuid(g));
                    pd.RemoveOverheadItem(stub);
                    made.RemoveAll(m => m.Id == g);
                    n++;
                }
                SaveMade();
                yield return null;
                if (cart.Cargo.Count > 0)
                {
                    log.LogWarning($"[director] {cart.Cargo.Count} item(s) still on the driven cart after taking {n} off by GUID.");
                }
                yield break;
            }
            cart.SetDownCargo();
            yield return null;
            RemoveMineFromGround();
            yield return null;
        }

        private RectInt CropRect(Camera cam, Vector3 at, float halfX, float height, float halfZ)
        {
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            for (int c = 0; c < 8; c++)
            {
                Vector3 corner = at + new Vector3((c & 1) == 0 ? -halfX : halfX, (c & 2) == 0 ? 0f : height, (c & 4) == 0 ? -halfZ : halfZ);
                Vector3 sp = cam.WorldToScreenPoint(corner);
                minX = Mathf.Min(minX, sp.x); maxX = Mathf.Max(maxX, sp.x);
                minY = Mathf.Min(minY, sp.y); maxY = Mathf.Max(maxY, sp.y);
            }
            int m = seriesMarginPx.Value;
            int x0 = Mathf.Clamp(Mathf.FloorToInt(minX) - m, 0, Screen.width - 2);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(minY) - m, 0, Screen.height - 2);
            int x1 = Mathf.Clamp(Mathf.CeilToInt(maxX) + m, x0 + 2, Screen.width);
            int y1 = Mathf.Clamp(Mathf.CeilToInt(maxY) + m, y0 + 2, Screen.height);
            int w = (x1 - x0) & ~1, h = (y1 - y0) & ~1; // even sizes (video encoders want them)
            return new RectInt(x0, y0, Mathf.Max(2, w), Mathf.Max(2, h));
        }

        // The screen at the end of this frame, cut to the rectangle, as a PNG. Null when written; else what went wrong.
        private string GrabCropped(Camera cam, RectInt crop, string path)
        {
            Texture2D full = null, cut = null;
            try
            {
                full = ScreenCapture.CaptureScreenshotAsTexture();
                // The screenshot may differ from the screen (a scaled camera rect): the crop is scaled with it.
                float sx = full.width / Mathf.Max(1f, Screen.width), sy = full.height / Mathf.Max(1f, Screen.height);
                int x0 = Mathf.Clamp(Mathf.RoundToInt(crop.x * sx), 0, full.width - 1);
                int y0 = Mathf.Clamp(Mathf.RoundToInt(crop.y * sy), 0, full.height - 1);
                int w = Mathf.Clamp(Mathf.RoundToInt(crop.width * sx), 1, full.width - x0);
                int h = Mathf.Clamp(Mathf.RoundToInt(crop.height * sy), 1, full.height - y0);
                cut = new Texture2D(w, h, TextureFormat.RGB24, false);
                cut.SetPixels(full.GetPixels(x0, y0, w, h));
                // The still background held: a pixel within the threshold of the previous frame keeps that frame's value
                // (lighting flicker, not content), so the GIF has only the real change to encode.
                int thr = holdStillBelow.Value;
                if (thr > 0)
                {
                    Color32[] now = cut.GetPixels32();
                    if (prevFrame != null && prevFrame.Length == now.Length)
                    {
                        for (int i = 0; i < now.Length; i++)
                        {
                            Color32 p = prevFrame[i], c = now[i];
                            if (Math.Abs(p.r - c.r) < thr && Math.Abs(p.g - c.g) < thr && Math.Abs(p.b - c.b) < thr)
                            {
                                now[i] = p;
                            }
                        }
                        cut.SetPixels32(now);
                    }
                    prevFrame = now;
                }
                cut.Apply();
                File.WriteAllBytes(path, cut.EncodeToPNG());
                return null;
            }
            catch (Exception e)
            {
                return "grab failed: " + e.Message;
            }
            finally
            {
                if (full != null) UnityEngine.Object.Destroy(full);
                if (cut != null) UnityEngine.Object.Destroy(cut);
            }
        }

        private void WriteGifScript(string dir, RectInt crop)
        {
            try
            {
                string secs = Mathf.Max(0.05f, seriesFrameSeconds.Value).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                string[] lines =
                {
                    "#!/bin/bash",
                    "# The frames of this series into one GIF. Pixel art: no smoothing, no dither. Arguments: the scale, the seconds per frame:",
                    "#   bash make-gif.sh            -> cargo.gif at half the frames' size (frames " + crop.width + "x" + crop.height + "; enough for the web), " + secs + " s per frame",
                    "#   bash make-gif.sh 1          -> the frames' full size (nearest neighbour either way)",
                    "#   bash make-gif.sh 0.5 0.4    -> half size, 0.4 s per frame",
                    "cd \"$(dirname \"$0\")\"",
                    "S=${1:-0.5}",
                    "T=${2:-" + secs + "}",
                    "ffmpeg -y -framerate \"1/$T\" -i %02d.png -vf \"scale=iw*$S:ih*$S:flags=neighbor,split[a][b];[a]palettegen=max_colors=256:stats_mode=diff[p];[b][p]paletteuse=dither=none\" -loop 0 cargo.gif",
                    "echo wrote $(pwd)/cargo.gif at $T s per frame",
                };
                File.WriteAllLines(Path.Combine(dir, "make-gif.sh"), lines);
            }
            catch (Exception e)
            {
                log.LogWarning("[director] writing make-gif.sh failed: " + e.Message);
            }
        }

        private void DropAtFeet(string id, int count)
        {
            PlayerData pd = MainGame.PlayerData;
            if (pd == null || MainGame.Instance == null)
            {
                return;
            }
            int n = 0;
            for (int i = 0; i < count; i++)
            {
                Item item = Make(id, out string note);
                if (item == null)
                {
                    Say($"{id}: {note}");
                    break;
                }
                Vector3 pos = pd.position.Value + new Vector3(0.7f * (i - (count - 1) * 0.5f), 0f, -0.6f);
                if (!MainGame.Instance.dropSystem.DropItem(item, pd.currentGameSceneId, pos))
                {
                    Say($"{id}: the game refused the drop");
                    break;
                }
                Remember(item, pd.currentGameSceneId);
                n++;
            }
            Say($"{n} {id} on the ground.");
            log.LogInfo($"[director] dropped {n} {id} at {pd.position.Value}");
        }

        // An item the way the game makes it: a body from the delivery script's body definition, the rest from its id.
        private static Item Make(string id, out string note)
        {
            note = null;
            ItemDef def = GameBalance.Me.GetDataOrNull<ItemDef>(id);
            if (def == null)
            {
                note = "unknown item";
                return null;
            }
            if (def.itemGroupIds.Contains("body"))
            {
                BodyDef body = GameBalance.Me.GetDataOrNull<BodyDef>("body_0_1");
                if (body != null)
                {
                    return body.GenerateItem();
                }
                note = "no body definition";
                return null;
            }
            return new Item(id);
        }

        private void ClaimCartLoad(MountHandle cart)
        {
            if (cart == null || !cart.Exists)
            {
                return;
            }
            LoadMade();
            int n = 0;
            foreach (CargoItem c in cart.Cargo)
            {
                if (Guid.TryParse(c.UniqueId, out Guid g) && !made.Exists(m => m.Id == g))
                {
                    made.Add((g, cart.SceneId));
                    n++;
                }
            }
            SaveMade();
            Say($"{n} item(s) on the cart claimed as the director's ({made.Count} listed).");
            log.LogInfo($"[director] claimed {n} cart item(s) as made");
        }

        // Everything of the director's: in the keeper's hands, lying in a scene, or on the companion's cart when the
        // whole load is the director's (set down first; the keeper gets off for it). Nothing else is ever touched.
        private IEnumerator RemoveMadeRoutine()
        {
            MountCompanion me = Companion();
            MountHandle cart = CartForLoading(me);
            if (cart != null && Driving(cart) && cart.Cargo.Count > 0 && AllMine(cart))
            {
                yield return ClearCart(cart); // off the driven cart by GUID, the keeper stays in the saddle
            }
            RemoveMade();
        }

        private void RemoveMade()
        {
            PlayerData pd = MainGame.PlayerData;
            if (pd == null || MainGame.Instance == null)
            {
                return;
            }
            int hands = 0, ground = 0, onCart = 0, gone = 0, setDown = 0;
            var cartIds = new HashSet<string>();
            var mine = new HashSet<string>();
            foreach ((Guid id, string _) in made)
            {
                mine.Add(id.ToString());
            }
            MountCompanion me = Companion();
            foreach (MountHandle h in new[] { me?.Donkey, me?.Cart })
            {
                if (h == null || !h.Exists || h.Cargo.Count == 0)
                {
                    continue;
                }
                // A load that is the director's through and through is set down at the cart (the API's own set-down,
                // which refuses a ridden cart: the routine got the keeper off first), so the drops below find it.
                // Anything of the player's aboard: the cart is left alone. Whatever stays aboard is listed as on the
                // cart, never forgotten (until 2026-10-14 a ridden cart was skipped here and its load forgotten).
                bool allMine = true;
                foreach (CargoItem c in h.Cargo)
                {
                    if (!mine.Contains(c.UniqueId))
                    {
                        allMine = false;
                    }
                }
                if (allMine && !h.IsRidden)
                {
                    setDown += h.SetDownCargo();
                }
                foreach (CargoItem c in h.Cargo)
                {
                    cartIds.Add(c.UniqueId);
                }
            }
            foreach ((Guid id, string scene) in new List<(Guid, string)>(made))
            {
                string key = id.ToString();
                Item inHands = null;
                foreach (Item it in pd.OverheadItems)
                {
                    if (it != null && it.UniqueId != null && it.UniqueId.Guid == id)
                    {
                        inHands = it;
                    }
                }
                if (inHands != null)
                {
                    pd.RemoveOverheadItem(inHands);
                    made.RemoveAll(m => m.Id == id);
                    hands++;
                    continue;
                }
                if (cartIds.Contains(key))
                {
                    onCart++;
                    continue;
                }
                DropData drop = null;
                GameSceneData gsd = null;
                try
                {
                    gsd = MainGame.Instance.GameSave.worldData.GetGameSceneDataById(scene);
                }
                catch
                {
                    gsd = null;
                }
                if (gsd != null)
                {
                    foreach (DropData d in gsd.droppedItems)
                    {
                        if (d != null && d.UniqueId != null && d.UniqueId.Guid == id)
                        {
                            drop = d;
                        }
                    }
                }
                if (drop != null)
                {
                    MainGame.Instance.dropSystem.RemoveDrop(drop, scene);
                    made.RemoveAll(m => m.Id == id);
                    ground++;
                    continue;
                }
                // Not in the hands, not on the companion's cart, not lying in its scene: used up, or carried elsewhere
                // (then it's the player's item now). Forgotten: the list only ever shrinks to what the director can see.
                made.RemoveAll(m => m.Id == id);
                gone++;
            }
            SaveMade();
            Say($"Removed {hands} from the hands and {ground} from the ground ({setDown} set down from the cart first); {onCart} still on the cart ({(onCart > 0 ? "a load of yours is aboard too, or the cart is ridden: set them down, then again" : "none")}); {gone} no longer found, forgotten.");
            log.LogInfo($"[director] remove what I made: hands {hands}, ground {ground} ({setDown} set down from the cart), on a mixed cart {onCart}, forgotten {gone}");
        }

        // The director's items lying in their scenes, taken back; how many.
        private int RemoveMineFromGround()
        {
            if (MainGame.Instance == null)
            {
                return 0;
            }
            int n = 0;
            foreach ((Guid id, string scene) in new List<(Guid, string)>(made))
            {
                GameSceneData gsd = null;
                try
                {
                    gsd = MainGame.Instance.GameSave.worldData.GetGameSceneDataById(scene);
                }
                catch
                {
                    gsd = null;
                }
                if (gsd == null)
                {
                    continue;
                }
                DropData drop = null;
                foreach (DropData d in gsd.droppedItems)
                {
                    if (d != null && d.UniqueId != null && d.UniqueId.Guid == id)
                    {
                        drop = d;
                    }
                }
                if (drop != null)
                {
                    MainGame.Instance.dropSystem.RemoveDrop(drop, scene);
                    made.RemoveAll(m => m.Id == id);
                    n++;
                }
            }
            if (n > 0)
            {
                SaveMade();
            }
            return n;
        }

        private static string MadeFile => Path.Combine(Paths.BepInExRootPath, "GK2-director-items.txt");

        private void Remember(Item item, string scene)
        {
            LoadMade();
            made.Add((item.UniqueId.Guid, scene));
            SaveMade();
        }

        private void Forget(Item item)
        {
            Guid g = item.UniqueId.Guid;
            made.RemoveAll(m => m.Id == g);
            SaveMade();
        }

        private void LoadMade()
        {
            if (madeLoaded)
            {
                return;
            }
            madeLoaded = true;
            try
            {
                if (!File.Exists(MadeFile))
                {
                    return;
                }
                foreach (string line in File.ReadAllLines(MadeFile))
                {
                    string[] p = line.Split('|');
                    if (p.Length == 2 && Guid.TryParse(p[0], out Guid g))
                    {
                        made.Add((g, p[1]));
                    }
                }
            }
            catch (Exception e)
            {
                log.LogWarning("[director] reading " + MadeFile + " failed: " + e.Message);
            }
        }

        private void SaveMade()
        {
            try
            {
                var lines = new List<string>();
                foreach ((Guid id, string scene) in made)
                {
                    lines.Add(id + "|" + scene);
                }
                File.WriteAllLines(MadeFile, lines);
            }
            catch (Exception e)
            {
                log.LogWarning("[director] writing " + MadeFile + " failed: " + e.Message);
            }
        }

        // ------------------------------------------------------------------ capture

        private void Capture(string name)
        {
            Run(CaptureRoutine(name));
        }

        private IEnumerator CaptureRoutine(string name)
        {
            Capturing = true;
            HidePanel();
            HUD hud = Hud();
            bool hid = false;
            if (hideHudForCapture.Value && hud != null && hud.gameObject.activeSelf)
            {
                SetHud(false);
                hid = true;
            }
            yield return null; // the window and the HUD are gone from the next frame
            yield return new WaitForEndOfFrame();
            string path = null;
            try
            {
                Directory.CreateDirectory(folder.Value);
                shotCounter++;
                path = Path.Combine(folder.Value, $"{name}_{DateTime.Now:yyyyMMdd_HHmmss}_{shotCounter:00}.png");
                ScreenCapture.CaptureScreenshot(path, superSize.Value);
            }
            catch (Exception e)
            {
                log.LogWarning("[director] capture failed: " + e.Message);
                path = null;
            }
            if (path != null)
            {
                for (int i = 0; i < 180 && !File.Exists(path); i++) // written by the engine, usually within a frame or two
                {
                    yield return null;
                }
                yield return null;
                string size = PngSize(path);
                Say($"Saved {Path.GetFileName(path)} ({size}).");
                log.LogInfo($"[director] screenshot {path}: {size}");
            }
            if (hid)
            {
                SetHud(true);
            }
            RestorePanel();
            Capturing = false;
        }

        private static string PngSize(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return "not written";
                }
                using (FileStream f = File.OpenRead(path))
                {
                    byte[] h = new byte[24];
                    if (f.Read(h, 0, 24) < 24)
                    {
                        return f.Length + " bytes";
                    }
                    int w = (h[16] << 24) | (h[17] << 16) | (h[18] << 8) | h[19];
                    int hh = (h[20] << 24) | (h[21] << 16) | (h[22] << 8) | h[23];
                    return $"{w}x{hh}, {f.Length / 1024} KB";
                }
            }
            catch (Exception e)
            {
                return "unreadable: " + e.Message;
            }
        }

        // ------------------------------------------------------------------ plumbing

        private void Run(IEnumerator routine)
        {
            if (running != null)
            {
                return;
            }
            running = host.StartCoroutine(Wrap(routine));
        }

        private IEnumerator Wrap(IEnumerator routine)
        {
            // Exceptions inside a coroutine would end it silently; they are logged and the state cleared.
            while (true)
            {
                object current;
                try
                {
                    if (!routine.MoveNext())
                    {
                        break;
                    }
                    current = routine.Current;
                }
                catch (Exception e)
                {
                    log.LogWarning("[director] a shot step failed: " + e);
                    Say("Failed: " + e.Message);
                    break;
                }
                yield return current;
            }
            RestorePanel();
            Capturing = false;
            running = null;
        }

        private void Say(string text)
        {
            status = text;
        }

        private static GUIStyle rich;
        private static GUIStyle Rich => rich ?? (rich = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true });

        // ------------------------------------------------------------------ the API's dev panel, out of the picture

        private static readonly FieldInfo PanelHidden = typeof(GK2DonkeyAPI.Plugin).GetField("TuningWindowHidden", BindingFlags.NonPublic | BindingFlags.Static);
        private bool? panelWasHidden;
        private bool panelLogged;

        private void HidePanel()
        {
            if (PanelHidden == null)
            {
                if (!panelLogged)
                {
                    panelLogged = true;
                    log.LogWarning("[director] Plugin.TuningWindowHidden not found in the API: the dev panel stays in the picture; close it with its key.");
                }
                return;
            }
            panelWasHidden = (bool)PanelHidden.GetValue(null);
            PanelHidden.SetValue(null, true);
        }

        private void RestorePanel()
        {
            if (PanelHidden != null && panelWasHidden.HasValue)
            {
                PanelHidden.SetValue(null, panelWasHidden.Value);
                panelWasHidden = null;
            }
        }

        // Driving this cart: the loads go through the game's hand calls, which the API reroutes onto the cart.
        private static bool Driving(MountHandle cart) => cart != null && cart.Exists && cart.IsRidden && Mounts.IsMounted && Mounts.Style == MountStyle.Cart;

        // Riding the donkey without the cart: nothing can be loaded without getting off, which a series must not do.
        private static string WhyNotNow(MountHandle cart)
        {
            if (Mounts.IsMounted && !Driving(cart))
            {
                return "Riding without the cart: hitch it (G) or get off first.";
            }
            return null;
        }
    }
}
