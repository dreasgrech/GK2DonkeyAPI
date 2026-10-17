using System;
using System.Collections.Generic;
using BepInEx;
using GK2DonkeyAPI.Api;
using UnityEngine;

namespace GK2DonkeySkins
{
    // Example mod: two skins for the donkey registered from code (docs/skins-design.md, docs/api.md "Looks").
    //   - "sepia": a recipe, a rule over the donkey's own frames (the API builds it from the game's art when first worn);
    //   - "checker": pictures drawn by code (IMountSkinSource), a placeholder that shows how a source works.
    // A skin pack needs none of this: a gk2skins/<id>/ folder with skin.cfg and PNGs anywhere under BepInEx/plugins is
    // found by the API itself (docs/skins.md). Register in Awake, and dispose the handles to withdraw.
    [BepInPlugin(Guid, "GK2 Donkey Skins (example)", "0.1.0")]
    [BepInDependency("dreas.gk2.donkeyapi", "0.30.0")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "dreas.gk2.donkeyapi.example.skins";

        private readonly List<IDisposable> registered = new List<IDisposable>();

        private void Awake()
        {
            if (Mounts.ApiVersion < new Version(0, 30, 0))
            {
                Logger.LogWarning("Skins example: GK2 Donkey API 0.30.0 or newer is needed.");
                return;
            }
            registered.Add(Mounts.Looks.RegisterRecipe(Guid, "sepia", "Sepia (example)", Sepia, author: "dreas", version: "0.1"));
            // A mount kind of our own (docs/mounts-design.md): the checker skin is a look of it. It can't pull the cart,
            // sits the rider two pixels higher, and carries one ability: it burns (the built-in Hellfire) while ridden.
            registered.Add(Mounts.Kinds.Register(Guid, new MountKindDef
            {
                Id = "checker-beast",
                Name = "Checker Beast",
                Author = "dreas",
                Version = "0.1",
                Cart = false,
                TuningRide = new Dictionary<string, object> { ["SeatAdjustPx"] = 2 }, // the key is an int (art pixels)
                Abilities = { new BurnsWhenRidden(Guid) },
            }));
            registered.Add(Mounts.Looks.Register(Guid, new MountSkinDef
            {
                Id = "checker",
                Name = "Checker (example)",
                Author = "dreas",
                Version = "0.1",
                Mount = "checker-beast",
                Source = new CheckerSource(),
            }));
            Logger.LogInfo("Skins example: 'sepia' and 'checker' registered (checker is a Checker Beast: no cart, burns while ridden); pick them in Saddle Up's Donkey settings.");
        }

        private void OnDestroy()
        {
            foreach (IDisposable h in registered)
            {
                h.Dispose(); // withdrawn: wearers show the donkey again and keep their choice
            }
            registered.Clear();
        }

        // The recipe: the donkey's own shading in warm brown tones, its outline kept dark. frame.Pixels is the frame on
        // its 96x96 canvas (Unity's order, the bottom row first); return the same layout.
        private static Color32[] Sepia(MountSkinFrame frame)
        {
            Color32[] src = frame.Pixels;
            var dst = new Color32[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                Color32 c = src[i];
                if (c.a == 0) continue;
                int x = i % frame.Width, y = i / frame.Width;
                if (frame.Rim(x, y))
                {
                    dst[i] = new Color32(40, 24, 12, c.a);
                    continue;
                }
                Color32 v = MountSkinFrame.Ramp(MountSkinFrame.Luminance(c) * 1.6f,
                    (0f, new Color32(60, 38, 20, 255)), (0.6f, new Color32(170, 120, 70, 255)), (1f, new Color32(235, 205, 160, 255)));
                v.a = c.a;
                dst[i] = v;
            }
            return dst;
        }

        // An ability: the instance's own Effect key set to Hellfire while the keeper rides it and cleared after (an
        // override owned by this mod, so a player's own pick on another donkey is untouched). The hooks run on the API's
        // LateUpdate; nothing here allocates per frame.
        private sealed class BurnsWhenRidden : MountAbility
        {
            private readonly string owner;

            internal BurnsWhenRidden(string owner)
            {
                this.owner = owner;
            }

            public override string Id => "burns-when-ridden";

            public override void Mounted(MountHandle mount, MountStyle style)
            {
                mount.Tuning.Set(owner, "Effect", "hellfire");
                mount.Tuning.Set(owner, "EffectWhen", MountEffectWhen.Always);
            }

            public override void Dismounted(MountHandle mount)
            {
                if (mount.Tuning.SetBy("Effect") == owner) mount.Tuning.Clear("Effect");
                if (mount.Tuning.SetBy("EffectWhen") == owner) mount.Tuning.Clear("EffectWhen");
            }

            public override void Detached(MountHandle mount) => Dismounted(mount);
        }

        // A source: one picture per frame key, made on demand and kept by the mod. Here every frame is the same 96x96
        // checkerboard oval, so the example needs no art; a real source would decode its own pictures.
        private sealed class CheckerSource : IMountSkinSource
        {
            private Texture2D tex;
            private Sprite sprite;

            // Every body frame gets the same picture (the part-level key "bdy" covers them all); no head picture.
            public IEnumerable<string> Keys => new[] { "bdy" };

            public bool TryGetFrame(string key, out Sprite result)
            {
                if (key != "bdy")
                {
                    result = null;
                    return false;
                }
                if (tex == null)
                {
                    tex = new Texture2D(96, 96, TextureFormat.RGBA32, false) { name = "checker skin", filterMode = FilterMode.Point };
                    var px = new Color32[96 * 96];
                    for (int y = 0; y < 96; y++)
                    {
                        for (int x = 0; x < 96; x++)
                        {
                            // an oval where the donkey's body is (its back on row 41 from the top, the ground on row 64)
                            float dx = (x - 50f) / 24f, dy = (95 - y - 52f) / 11f;
                            bool inside = dx * dx + dy * dy <= 1f;
                            bool edge = inside && dx * dx + dy * dy > 0.82f;
                            px[y * 96 + x] = !inside ? new Color32(0, 0, 0, 0) : edge ? new Color32(30, 30, 30, 255)
                                : ((x / 6 + y / 6) & 1) == 0 ? new Color32(220, 60, 60, 255) : new Color32(240, 230, 200, 255);
                        }
                    }
                    tex.SetPixels32(px);
                    tex.Apply(false, true);
                    // the donkey's measures: 50 pixels per unit, the anchor at the picture's centre
                    sprite = Sprite.Create(tex, new Rect(0f, 0f, 96f, 96f), new Vector2(0.5f, 0.5f), 50f, 0, SpriteMeshType.FullRect);
                    sprite.name = "checker skin bdy";
                }
                result = sprite;
                return true;
            }
        }
    }
}
