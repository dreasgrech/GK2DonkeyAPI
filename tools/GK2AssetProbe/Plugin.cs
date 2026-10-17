using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace GK2AssetProbe
{
    // Dev-only: can art from outside the game be drawn in it, and how does it look (docs/external-assets.md,
    // docs/mount-looks-design.md)? A window (Shift+F7; F8 and F9 with Shift are the perf runner's and the showcase
    // director's, F9 and Ctrl+F9 Performance Hog's) in tabs:
    //   Skins     dress the ridden donkey with a test skin (over it or instead of it), pick the skin, export its frames;
    //   Fire      the game's own lamp fire round the donkey and at its steps (ring or trail, size);
    //   My files  your own pictures and sounds from test\;
    //   Capture   film the donkey and its rider for a few seconds (for the preview page);
    //   Game      what the game has: its asset names, shaders, effects;
    //   Old tests the first fire experiments (our own particles, the game's flame materials).
    // The answers go to the log with the tag [assetprobe] and to files in BepInEx/GK2-asset-probe/. Reads the donkey,
    // never changes it beyond the dressing (put back when it ends): everything it draws is its own object, and "Clear
    // all" (or leaving the scene) removes it. Never touches the save.
    [BepInPlugin(Guid, "GK2 Asset Probe", "0.2.0")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "dreas.gk2.assetprobe";

        private ConfigEntry<KeyboardShortcut> toggleKey;
        private bool open, closeNow;
        private Rect window = new Rect(40f, 60f, 460f, 10f);
        private string fxName = "puff_npc";
        private int tab;
        private static readonly string[] Tabs = { "Skins", "Fire", "My files", "Capture", "Game", "Old tests" };
        private GUIStyle head, note, status;

        private void Awake()
        {
            toggleKey = Config.Bind("General", "ToggleKey", new KeyboardShortcut(KeyCode.F7, KeyCode.LeftShift), "Opens and closes the probe's window.");
            Probe.Init(Logger);
            StartCoroutine(EndOfFrames());
            useGUILayout = false;
            Logger.LogInfo($"[assetprobe] ready; {toggleKey.Value} opens the window.");
        }

        private void Update()
        {
            if (toggleKey.Value.IsDown())
            {
                open = !open;
            }
            if (closeNow)
            {
                closeNow = false;
                open = false;
            }
            useGUILayout = (open || Probe.HasLabels) && !Capture.Recording;
            Probe.Tick();
        }

        private void LateUpdate() => Probe.Follow();

        // After every camera and the GUI of each frame: whatever the dressing hid shows again before game code runs.
        private System.Collections.IEnumerator EndOfFrames()
        {
            var wait = new WaitForEndOfFrame();
            while (true)
            {
                yield return wait;
                MyArt.EndOfFrame();
            }
        }

        private void OnDestroy() => Probe.ClearAll();

        private void OnGUI()
        {
            if (Capture.Recording)
            {
                return; // nothing of ours in the film
            }
            Probe.DrawLabels();
            if (!open)
            {
                return;
            }
            if (head == null)
            {
                head = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 13 };
                note = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true };
                note.normal.textColor = new Color(0.75f, 0.75f, 0.7f);
                status = new GUIStyle(GUI.skin.box) { wordWrap = true, alignment = TextAnchor.MiddleLeft, fontSize = 12 };
            }
            window = GUILayout.Window(0x6b2a7e, window, Draw, "GK2 Asset Probe");
        }

        private void Draw(int id)
        {
            GUILayout.Label(Probe.Status, status, GUILayout.MinHeight(34f));
            GUILayout.Space(4f);
            int t = GUILayout.Toolbar(tab, Tabs);
            if (t != tab)
            {
                tab = t;
                window.height = 10f; // shrink to the new tab's rows
            }
            GUILayout.Space(6f);
            switch (tab)
            {
                case 0: DrawSkins(); break;
                case 1: DrawFire(); break;
                case 2: DrawFiles(); break;
                case 3: DrawCapture(); break;
                case 4: DrawGame(); break;
                default: DrawOld(); break;
            }
            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear all")) Probe.ClearAll();
            if (GUILayout.Button("Close")) closeNow = true;
            GUILayout.EndHorizontal();
            GUI.DragWindow();
        }

        private void DrawSkins()
        {
            GUILayout.Label("Dress the ridden donkey", head);
            int d = GUILayout.Toolbar((int)MyArt.Dress, new[] { "Off", "Over the donkey", "Instead of it" });
            if (d != (int)MyArt.Dress) MyArt.SetDress((MyArt.Mode)d);
            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Skin:", GUILayout.Width(40f));
            if (GUILayout.Button("<", GUILayout.Width(30f))) MyArt.PrevSkin();
            GUILayout.Label(MyArt.SkinName, head, GUILayout.MinWidth(120f));
            if (GUILayout.Button(">", GUILayout.Width(30f))) MyArt.NextSkin();
            if (GUILayout.Button("Reload pictures")) MyArt.ForgetFrames();
            GUILayout.EndHorizontal();
            GUILayout.Label("Skins are folders in test\\skins\\ (test\\donkey\\ is the first). Pictures are named like donkey-frames\\.", note);
            GUILayout.Space(6f);
            GUILayout.Label("The donkey's frames", head);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Export every frame")) MyArt.ExportAllFrames();
            if (GUILayout.Button("Art spec (30 s sample)")) Probe.ArtSpec();
            GUILayout.EndHorizontal();
            GUILayout.Label("Saved to donkey-frames\\: templates to paint over, and the recipes' input.", note);
        }

        private void DrawFire()
        {
            GUILayout.Label("The game's own lamp fire", head);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(LampFlames.RingOn ? "Ring: ON" : "Ring: off")) LampFlames.ToggleRing();
            if (GUILayout.Button(LampFlames.StepsOn ? "Steps: ON" : "Steps: off")) LampFlames.ToggleSteps();
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Ring:", GUILayout.Width(40f));
            int look = GUILayout.Toolbar(LampFlames.Trail ? 1 : 0, new[] { "Stays with the donkey", "Trails behind" });
            LampFlames.SetTrail(look == 1);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Size:", GUILayout.Width(40f));
            LampFlames.SetSize(GUILayout.Toolbar(LampFlames.SizeIndex, LampFlames.SizeLabels));
            GUILayout.EndHorizontal();
            GUILayout.Label("Copies the flame and sparks of a lamp in memory: a lit lamp nearby, or the cart's lantern lit.", note);
        }

        private void DrawFiles()
        {
            GUILayout.Label("Your own art and sounds", head);
            if (GUILayout.Button("Show my pictures beside the donkey (test\\*.png)")) MyArt.ShowPictures();
            if (GUILayout.Button("Play my next sound (test\\*.ogg, *.wav)")) MyArt.NextSound();
            if (GUILayout.Button("Test card, drawn five ways (lighting check)")) Probe.TestPictures();
            GUILayout.Label("Files are read again on every press: change them while the game runs.", note);
        }

        private void DrawCapture()
        {
            GUILayout.Label("Film the donkey and its rider", head);
            if (GUILayout.Button(Capture.Recording ? "Recording..." : "Record 4 seconds")) Capture.Start(this);
            GUILayout.Label("Ride or stand the way you want it filmed. The window hides while recording; the pictures go to captures\\, and tools/skin_preview_html.py plays them.", note);
            if (Capture.Last.Length > 0) GUILayout.Label("Last: captures\\" + Capture.Last, note);
        }

        private void DrawGame()
        {
            GUILayout.Label("What the game has", head);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Dump asset names")) Probe.DumpKeys();
            if (GUILayout.Button("Shaders")) Probe.Shaders();
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Effect:", GUILayout.Width(50f));
            fxName = GUILayout.TextField(fxName ?? "", GUILayout.Width(170f));
            if (GUILayout.Button("Play at the donkey")) Probe.PlayGameFx(fxName.Trim());
            GUILayout.EndHorizontal();
            GUILayout.Label("Effect names: effects.txt (written by Dump asset names).", note);
        }

        private void DrawOld()
        {
            GUILayout.Label("The first fire experiments", head);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Probe.FlamesOn ? "Flames: ON" : "Flames: off")) Probe.ToggleFlames();
            if (GUILayout.Button(Probe.StepsOn ? "Steps: ON" : "Steps: off")) Probe.ToggleSteps();
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Next shader (" + Probe.EffectShaderName + ")")) Probe.NextEffectShader();
            if (GUILayout.Button("Add the game's flame materials to the shaders")) Probe.GameFlames();
            GUILayout.Label("Our own particles with test shaders; superseded by the Fire tab.", note);
        }
    }
}
