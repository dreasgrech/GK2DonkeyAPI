using System;
using System.Collections.Generic;
using GK2DonkeyAPI.Api;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // The Mounts tab (docs/mounts-design.md §6): one mount at a time, what you ride by default. The rows are the
    // mount's own values (its mount's: the rider's seat per style, the cart, foals, an effect; its skin's: the
    // pictures' offsets, the foals), kept apart from your settings on the other tabs: a value set here belongs to every
    // donkey that is this mount, in every save, and your own values (Riding tab) are untouched. Yellow = set for
    // the mount; right-click = not set (your value applies). Changes show on the ridden donkey at once; "Save" writes
    // them into the mount's skin.cfg (a pack's own file, a built-in's in the repo on the machine with the sources).
    internal static partial class TuningWindow
    {
        private static string mountsSkinId;           // the mount being tuned (a skin id, "" = the donkey)
        private static bool mountsFollowRide = true;  // follow what is ridden until the user picks one
        private static string mountsNote = "";
        private static float mountsNoteUntil;

        // The seat, leg and cart keys a SKIN sets for its drawing (reference.md [Ride] / [Cart]; the skin owns its
        // geometry, docs/mounts-parity-matrix.md 9.1), with the ranges the Riding tab uses. CartOnly: the key means
        // nothing while riding plain, so its row shows for the cart style only.
        private static readonly (string Key, string Label, string Tip, float Min, float Max, bool Int, bool CartOnly)[] SkinKeys =
        {
            ("RiderOffsetPx", "Whole rider along the mount: tail (-) / head (+)", "Art pixels. The same key as the Riding tab's; this value replaces yours while the mount is this one.", -16, 16, true, false),
            ("TorsoOffsetPx", "Torso only along the mount: tail (-) / head (+)", "Art pixels; the legs stay put.", -16, 16, true, false),
            ("SeatAdjustPx", "Torso down (-) / up (+)", "Art pixels on top of the seat: the skin's seat row or picture top (declared above), else the player's automatic seat (AutoSeat on), else nothing: the torso height row is the seat then.", -16, 16, true, false),
            ("SeatOffset", "Torso height (AutoSeat off)", "World units above the ground, the seat most players have (AutoSeat is off by default): what the mount's seat should be for its drawing. Ignored on a skin with seat = picture.", 0f, 1.5f, false, false),
            ("LegAdjustXPx", "Legs along the mount: head (-) / tail (+)", "Art pixels, relative to the torso.", -8, 8, true, false),
            ("LegAdjustYPx", "Legs down (-) / up (+)", "Art pixels, relative to the back line.", -8, 8, true, false),
            ("LegHeightPx", "Rows of the body frame that are legs", "How many bottom rows of the keeper's frame are cut as legs (feet to belt).", 0, 30, true, false),
            ("BackSeatExtraPx", "Seen from behind (armour): seat lifted by", "Art pixels on top of the seat when the rider faces up in armour.", 0, 12, true, false),
            ("HitchGapPx", "Cart further behind (+) / closer (-)", "Art pixels. The whole cart, shafts included, sits this much further behind the mount than behind the donkey, so a longer body isn't drawn inside the cart. The cart stays where it was drawn when it is left and taken again.", -32, 64, true, true),
        };

        // The creature's own keys a mount sets (speed; its sounds are listed by OtherMountKeys).
        private static readonly (string Key, string Label, string Tip, float Min, float Max, bool Int, bool CartOnly)[] MountKeys =
        {
            ("SpeedMultiplier", "Speed (x normal walking)", "How fast this mount goes in this style, times the keeper's walking speed (the donkey: 2 riding, 1.5 with the cart): the mount's speed = in its [Mount] block (with the cart, times the donkey's cart-to-riding ratio).", 0.5f, 4f, false, false),
            ("WalkCycleSpeed", "Stride (walk clip rate per unit of speed)", "How fast the walk animation plays for each unit of walking speed: the donkey's is 1.5. Less for a longer stride, more for a scurry, so the legs match the speed (docs/mounts-foundation-2.md 2.4).", 0.5f, 4f, false, false),
        };

        private static void DrawMounts()
        {
            Looks.SkinDef skin = PickMount();
            Catalog.Entry mount = skin != null ? skin.Mount : Catalog.Donkey;
            string title = MountLabel(skin, mount);
            bool ride = PanelStyle == MountStyle.Ride;
            Mount.Instance r = Mount.Ridden;
            bool ridingIt = r != null && string.Equals(r.LooksRig.Wearing, skin?.Id ?? "", StringComparison.OrdinalIgnoreCase);

            Header("Tuning: " + title);
            GUILayout.BeginHorizontal();
            GUILayout.Label(Tip("Mount and skin", "Whose values the rows below are: a mount (Donkey, Dragon, ...) and one of its skins. It follows what you ride until you pick one here; 'Follow my ride' goes back to that."), label, Wd(LabelW));
            IReadOnlyList<(string Id, string Label)> choices = Looks.SkinChoices();
            int idx = 0;
            for (int i = 0; i < choices.Count; i++)
            {
                if (string.Equals(choices[i].Id, skin?.Id ?? MountLooks.DonkeyId, StringComparison.OrdinalIgnoreCase)) idx = i;
            }
            if (GUILayout.Button("<", Wd(22f))) { idx = (idx - 1 + choices.Count) % choices.Count; mountsSkinId = choices[idx].Id; mountsFollowRide = false; }
            GUILayout.Label(choices.Count > 0 ? choices[idx].Label : "-", label, Wd(SliderW - 44f));
            if (GUILayout.Button(">", Wd(22f))) { idx = (idx + 1) % choices.Count; mountsSkinId = choices[idx].Id; mountsFollowRide = false; }
            GUILayout.EndHorizontal();
            if (!mountsFollowRide && GUILayout.Button(Tip("Follow my ride", "The rows show the mount you ride, and change with it."), Wd(LabelW)))
            {
                mountsFollowRide = true;
            }
            GUILayout.Label(ridingIt
                ? "You ride this mount and skin now: every change shows on it at once."
                : r != null ? $"You ride a {MountLabel(Looks.Find(r.LooksRig.Wearing), r.LooksRig.MountNow)}; ride this one to see these changes live (Saddle Up: Mount, Skin)."
                : "Nothing is ridden: pick this skin in Saddle Up (Mount, Skin) and ride it to see the changes live.", small);
            if (EditingNow)
            {
                GUILayout.Label($"Note: {editing.Describe()} is being edited on the Instances tab. This tab never changes that one donkey: it changes the mount and skin, and a value that donkey has of its own (magenta there) still wins on it.", small);
            }
            GUILayout.Label("A mount (Donkey, Giant Rat, Dragon) has skins (the donkey: Default, Hellfire). The mount's rows are shared by all its skins; the skin's rows are that look's only. They belong to the mount and skin, not to you: every donkey that is them uses these values, in every save and for every player who has the file. Your own settings on the other tabs are untouched; a value set here replaces yours only while a donkey is this mount. Yellow = set here; right-click a row = not set (yours applies).", small);

            if (mount == Catalog.Donkey)
            {
                Header("The mount: Donkey (the game's own)");
                GUILayout.Label("The donkey mount has no values of its own: the Riding tab is where it is tuned. Its skins (Hellfire, ...) can still move their pictures below.", small);
            }
            else
            {
                DrawMountRows(mount, skin, ride);
            }
            if (skin != null)
            {
                DrawSkinRows(skin, mount, ride);
            }
            DrawMountSaving(skin, mount);
        }

        private static Looks.SkinDef PickMount()
        {
            Mount.Instance r = Mount.Ridden;
            if (mountsFollowRide && r != null)
            {
                mountsSkinId = r.LooksRig.Wearing;
            }
            return string.IsNullOrEmpty(mountsSkinId) ? null : Looks.Find(mountsSkinId);
        }

        // "Dragon (skin: Red Dragon)", "Donkey (the game's own skin)": the mount, then its skin, in every sentence.
        private static string MountLabel(Looks.SkinDef skin, Catalog.Entry mount)
        {
            if (skin == null) return "Donkey (the game's own skin)";
            string name = string.IsNullOrEmpty(skin.Name) ? skin.Id : skin.Name;
            return mount.Label + " (skin: " + name + ")";
        }

        // ---- the mount's rows

        private static void DrawMountRows(Catalog.Entry mount, Looks.SkinDef skin, bool ride)
        {
            var skins = Catalog.SkinsOf(mount);
            var names = new List<string>();
            foreach (Looks.SkinDef s in skins) names.Add(string.IsNullOrEmpty(s.Name) ? s.Id : s.Name);
            Header("The mount: " + mount.Label + (skins.Count > 1 ? "  (shared by " + string.Join(", ", names) + ")" : ""));
            GUILayout.Label(mount.FromPack
                ? "Declared by its skin.cfg ([Mount]). The rows are what the file says, until you save."
                : $"Registered in code by {mount.Owner}{(mount.Abilities.Length > 0 ? $", with {mount.Abilities.Length} abilit{(mount.Abilities.Length == 1 ? "y" : "ies")}" : "")}: changes here last this session (the mod's code is where they live).", small);

            if (OnOffRow("Pulls the cart", mount.Cart, mount.Cart ? label : changed, tip: "Off: the cart style, hitching and the style switch are refused for this mount, by name."))
            {
                mount.Cart = !mount.Cart;
                Catalog.Touch(mount);
            }
            if (OnOffRow("Flies", mount.Flies, mount.Flies ? changed : label, tip: "Ridden plain it takes off and flies (the flying experiment; one key for jump and fly: a second press in the air). Saved as flies in the mount's file."))
            {
                mount.Flies = !mount.Flies;
                Catalog.Touch(mount);
            }
            if (OnOffRow("Jumps", mount.Jumps, mount.Jumps ? label : changed, tip: "Off: the jump key does nothing on it (or takes off, if it flies). Saved as jumps in the mount's file."))
            {
                mount.Jumps = !mount.Jumps;
                Catalog.Touch(mount);
            }
            if (OnOffRow("Babies can be it", mount.Foals, mount.Foals ? label : changed, tip: "Off: the babies stay plain donkeys whatever the parent is."))
            {
                mount.Foals = !mount.Foals;
                Catalog.Touch(mount);
            }
            if (mount.Foals)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(Tip("Babies' size (x the grown one)", "The view's scale of a baby: the donkey's foals are 0.60; a mount drawn three times its creature's size (Gerry) wants 0.33 so its babies are the creature's own size. Saddle Up's babies take the new size within a second. Saved as foal_scale in the mount's file."), Mathf.Abs(mount.FoalScale - 0.6f) > 0.001f ? changed : label, Wd(LabelW));
                float size = GUILayout.HorizontalSlider(mount.FoalScale, 0.2f, 1f, Wd(SliderW - 50f));
                GUILayout.Label(mount.FoalScale.ToString("0.00"), label, Wd(50f));
                GUILayout.EndHorizontal();
                size = Mathf.Round(size * 100f) / 100f;
                if (Mathf.Abs(size - mount.FoalScale) > 0.001f)
                {
                    mount.FoalScale = Mathf.Clamp(size, 0.2f, 1f);
                    Catalog.Touch(mount);
                }
            }

            DrawMountJump(mount);

            Header("Rider on this mount: " + (ride ? "riding" : "with the cart") + "  (" + (ride ? "mount.cfg [Tuning.Ride]" : "mount.cfg [Tuning.Cart]") + ")");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Each style has its own values; the other style's rows show after a style switch.", small);
            if (skin != null && skin.SeatFromPicture) GUILayout.Label("This skin says seat = picture: the rider sits on its own picture, this frame's top, and 'Torso down / up' adjusts from there. The torso height row is ignored on it.", small);
            else if (skin != null && skin.SeatRow >= 0) GUILayout.Label($"This skin says seat = {skin.SeatRow}: the rider's hip sits on row {skin.SeatRow} of its picture for every player, whatever their AutoSeat, and 'Torso down / up' adjusts from there. The torso height row is ignored on it.", small);
            else if (!Plugin.For(ride ? MountStyle.Ride : MountStyle.Cart).AutoSeat.Entry.Value) GUILayout.Label("Your Riding tab has AutoSeat off (the default for everyone): the seat is the torso height, and 'Torso down / up' does nothing. Set the mount's torso height here, or give its skin seat = picture.", small);
            if (Mount.IsMounted && GUILayout.Button(Tip(ride ? "Go to cart" : "Go to riding", "Dismounts and mounts again in the other style, in place, as on the Riding tab."), Wd(SliderW)))
            {
                Mounts.SwitchStyle(ride ? MountStyle.Cart : MountStyle.Ride);
            }
            GUILayout.EndHorizontal();
            Dictionary<string, object> mine = ride ? mount.TuningRide : mount.TuningCart;
            Dictionary<string, object> both = mount.Tuning;
            GUILayout.Label("The rider's seat, legs and the cart's place are the skin's rows below (the skin owns its drawing's geometry); the mount keeps what the creature is.", small);
            foreach (var k in MountKeys)
            {
                if (k.CartOnly && ride) continue;
                MountValueRow(mount, ride, k.Key, k.Label, k.Tip, k.Min, k.Max, k.Int);
            }
            MountBoolRow(mount, ride, "NearLeg", "Rider's legs drawn in front of the mount", "His own baked-in legs hidden, a leg overlay drawn in front (the Riding tab's NearLeg).");
            OtherMountKeys(mount, ride);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Tip(ride ? "Copy these to the cart style" : "Copy these to riding", "The rows set for this style become the other style's too."), Wd(LabelW)))
            {
                var from = ride ? mount.TuningRide : mount.TuningCart;
                if (from != null && from.Count > 0)
                {
                    var to = new Dictionary<string, object>(from, StringComparer.OrdinalIgnoreCase);
                    if (ride) mount.TuningCart = to; else mount.TuningRide = to;
                    Catalog.Touch(mount);
                    Note("Copied.");
                }
            }
            if (GUILayout.Button(Tip("Clear this style", "Every row of this style back to not set."), Wd(SliderW)))
            {
                if (ride) mount.TuningRide = null; else mount.TuningCart = null;
                Catalog.Touch(mount);
            }
            GUILayout.EndHorizontal();
        }

        // The mount's own jump (mount.cfg [Jump], JumpShapes.cs): a row per key, the default jump's value shown (plain)
        // until dragged; a right-click on the slider takes the key back to the default's. The donkey's jump is the default
        // jump: the Flying tab's rows.
        private static void DrawMountJump(Catalog.Entry mount)
        {
            Header("Its jump  (mount.cfg [Jump]; riding plain)");
            if (mount == Catalog.Donkey)
            {
                GUILayout.Label("The donkey's jump is the default jump: the Flying tab's rows (every mount without a [Jump] key of its own takes them).", small);
                return;
            }
            GUILayout.Label("Not set: the default jump's value (the Flying tab's), plain. Drag to give this mount its own; right-click a slider to take it back to the default's. The next jump takes it.", small);
            foreach (JumpShapes.Key k in JumpShapes.Keys)
            {
                bool set = mount.Jump != null && mount.Jump.TryGetValue(k.Name, out float own);
                float value = set ? mount.Jump[k.Name] : JumpShapes.Default(k.Name);
                if (k.Bool)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(Tip(k.Label, k.Tip + " Saved as " + k.Name + " in [Jump]." + (set ? " Right-click: back to the default's." : " Not set for the mount: the default jump's value is shown.")), set ? changed : label, Wd(LabelW));
                    bool clicked = GUILayout.Button(value > 0.5f ? "On" : "Off", Wd(70f));
                    Rect br = GUILayoutUtility.GetLastRect();
                    bool unsetB = set && GUI.enabled && Event.current.type == EventType.MouseDown && Event.current.button == 1 && br.Contains(Event.current.mousePosition);
                    GUILayout.EndHorizontal();
                    if (unsetB)
                    {
                        Event.current.Use();
                        mount.Jump.Remove(k.Name);
                        if (mount.Jump.Count == 0) mount.Jump = null;
                        Catalog.Touch(mount);
                    }
                    else if (clicked)
                    {
                        (mount.Jump ?? (mount.Jump = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)))[k.Name] = value > 0.5f ? 0f : 1f;
                        Catalog.Touch(mount);
                    }
                    continue;
                }
                GUILayout.BeginHorizontal();
                GUILayout.Label(Tip(k.Label, k.Tip + " Saved as " + k.Name + " in [Jump]." + (set ? "" : " Not set for the mount: the default jump's value is shown.")), set ? changed : label, Wd(LabelW));
                float v = GUILayout.HorizontalSlider(value, k.Min, k.Max, Wd(SliderW - 84f));
                Rect r = GUILayoutUtility.GetLastRect();
                bool unset = GUI.enabled && Event.current.type == EventType.MouseDown && Event.current.button == 1 && r.Contains(Event.current.mousePosition);
                GUILayout.Label(value.ToString("0.00"), label, Wd(50f));
                GUILayout.EndHorizontal();
                if (unset)
                {
                    Event.current.Use();
                    if (set)
                    {
                        mount.Jump.Remove(k.Name);
                        if (mount.Jump.Count == 0) mount.Jump = null;
                        Catalog.Touch(mount);
                    }
                    continue;
                }
                if (v == Mathf.Clamp(value, k.Min, k.Max)) continue; // only a drag is a change
                v = (float)Math.Round(v, 2);
                if (set && Mathf.Abs(v - value) <= 0.0001f) continue;
                (mount.Jump ?? (mount.Jump = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)))[k.Name] = v;
                Catalog.Touch(mount);
            }
            if (mount.Jump != null && GUILayout.Button(Tip("Back to the default jump", "Every [Jump] key of this mount cleared: it jumps as the donkey does."), Wd(SliderW)))
            {
                mount.Jump = null;
                Catalog.Touch(mount);
            }
        }

        // What a row shows: the style's value, else the both-styles value, else the player's own (plain, not set).
        private static bool MountHas(Catalog.Entry mount, bool ride, string key, out object value)
        {
            if (key == SpeedKey)
            {
                float? own = Catalog.OwnSpeed(mount, ride ? MountStyle.Ride : MountStyle.Cart); // the mount's speed (with the cart times the donkey's ratio), over a pin in its tuning
                if (own.HasValue)
                {
                    value = own.Value;
                    return true;
                }
            }
            Dictionary<string, object> mine = ride ? mount.TuningRide : mount.TuningCart;
            if (mine != null && mine.TryGetValue(key, out value)) return true;
            if (mount.Tuning != null && mount.Tuning.TryGetValue(key, out value)) return true;
            value = null;
            return false;
        }

        // The speed row is the mount's speed (in the cart style the row shows and sets it through the donkey's ratio),
        // never a SpeedMultiplier in its tuning (SkinsScan refuses one, and so does the merge). A mount's speed is one of
        // the API's words: the slider snaps to the word its value reads as.
        private const string SpeedKey = "SpeedMultiplier";

        private static void MountSet(Catalog.Entry mount, bool ride, string key, object value)
        {
            if (key == SpeedKey)
            {
                float s = Convert.ToSingle(value, System.Globalization.CultureInfo.InvariantCulture);
                mount.Speed = Catalog.BandOf(s, ride ? MountStyle.Ride : MountStyle.Cart);
                (ride ? mount.TuningRide : mount.TuningCart)?.Remove(key);
                mount.Tuning?.Remove(key);
                Catalog.Touch(mount);
                return;
            }
            Dictionary<string, object> mine = ride ? mount.TuningRide : mount.TuningCart;
            if (mine == null)
            {
                mine = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                if (ride) mount.TuningRide = mine; else mount.TuningCart = mine;
            }
            mine[key] = value;
            Catalog.Touch(mount);
        }

        private static void MountUnset(Catalog.Entry mount, bool ride, string key)
        {
            if (key == SpeedKey)
            {
                mount.Speed = null;
            }
            (ride ? mount.TuningRide : mount.TuningCart)?.Remove(key);
            mount.Tuning?.Remove(key);
            Catalog.Touch(mount);
        }

        private static float PlayerValue(bool ride, string key)
        {
            try
            {
                MountTuning t = Mounts.Tuning(ride ? MountStyle.Ride : MountStyle.Cart);
                Type type = t.TypeOf(key);
                if (type == typeof(int)) return t.Get<int>(key);
                if (type == typeof(float)) return t.Get<float>(key);
                if (type == typeof(bool)) return t.Get<bool>(key) ? 1f : 0f;
            }
            catch
            {
            }
            return 0f;
        }

        private static void MountValueRow(Catalog.Entry mount, bool ride, string key, string name, string tip, float min, float max, bool isInt)
        {
            bool set = MountHas(mount, ride, key, out object raw);
            float value = set ? Convert.ToSingle(raw, System.Globalization.CultureInfo.InvariantCulture) : PlayerValue(ride, key);
            GUILayout.BeginHorizontal();
            GUILayout.Label(Tip(name, tip + (set ? "" : " Not set for the mount: your own value is shown. Drag to set it.")), set ? changed : label, Wd(LabelW));
            float v = GUILayout.HorizontalSlider(value, min, max, Wd(SliderW - 84f)); // room for the number and the x
            Rect r = GUILayoutUtility.GetLastRect();
            bool unset = GUI.enabled && Event.current.type == EventType.MouseDown && Event.current.button == 1 && r.Contains(Event.current.mousePosition);
            GUILayout.Label(isInt ? Mathf.RoundToInt(value).ToString() : value.ToString("0.00"), label, Wd(50f));
            GUILayout.EndHorizontal();
            if (unset)
            {
                Event.current.Use();
                if (set) MountUnset(mount, ride, key);
                return;
            }
            // only a drag is a change: the slider hands back the value it was given (clamped) when it isn't touched
            bool touched = v != Mathf.Clamp(value, min, max);
            if (!touched) return;
            if (isInt)
            {
                int iv = Mathf.RoundToInt(v);
                if (!set || iv != Mathf.RoundToInt(value)) MountSet(mount, ride, key, iv);
            }
            else
            {
                v = (float)Math.Round(v, 2);
                if (!set || Mathf.Abs(v - value) > 0.0001f) MountSet(mount, ride, key, v);
            }
        }

        // Keys a mount sets that the rows above don't show (any tuning key may be set in its file or by code): listed,
        // so nothing a mount does is hidden here.
        private static void OtherMountKeys(Catalog.Entry mount, bool ride)
        {
            var shown = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "NearLeg" };
            foreach (var k in MountKeys) shown.Add(k.Key);
            // a pixel key still on a mount (an older pack): listed here, since the scan refuses it for a built-in
            var parts = new List<string>();
            void Add(Dictionary<string, object> d, string where)
            {
                if (d == null) return;
                foreach (var kv in d) if (!shown.Contains(kv.Key)) parts.Add($"{kv.Key} = {kv.Value} ({where})");
            }
            Add(mount.Tuning, "both styles");
            Add(mount.TuningRide, "riding");
            Add(mount.TuningCart, "cart");
            if (parts.Count > 0) GUILayout.Label("Also set for this mount (its file or code; not editable here): " + string.Join("; ", parts) + ".", small);
        }

        private static void MountBoolRow(Catalog.Entry mount, bool ride, string key, string name, string tip)
        {
            bool set = MountHas(mount, ride, key, out object raw);
            bool value = set ? Convert.ToBoolean(raw) : PlayerValue(ride, key) > 0.5f;
            GUILayout.BeginHorizontal();
            GUILayout.Label(Tip(name, tip + (set ? "" : " Not set for the mount: your own value is shown.")), set ? changed : label, Wd(LabelW));
            if (GUILayout.Button(value ? "On" : "Off", Wd(70f))) MountSet(mount, ride, key, !value);
            if (set && GUILayout.Button(Tip("x", "Back to not set: your value applies (the sliders above do the same on a right-click)."), Wd(24f))) MountUnset(mount, ride, key);
            GUILayout.EndHorizontal();
        }

        // ---- the skin's rows

        private static void DrawSkinRows(Looks.SkinDef skin, Catalog.Entry mount, bool ride)
        {
            DrawSkinOffsets(skin, mount);
            DrawSkinSeat(skin, mount);
            DrawSkinTuning(skin, mount, ride);
            DrawSoundRows(skin, mount, ride, ofSkin: true);
        }

        // ---- the sounds (docs/mount-sounds-design.md): one row per event, the line's source as written in the
        // file's block (skin.cfg [Sounds] / mount.cfg [Sounds], or the style's section when the key is there), set here and
        // written back by the auto-save; the resolved result (what plays on the ridden instance, and its layer) next to it.

        private static readonly Dictionary<string, string> soundEdits = new Dictionary<string, string>(StringComparer.Ordinal);

        private static void DrawSoundRows(Looks.SkinDef skin, Catalog.Entry mount, bool ride, bool ofSkin)
        {
            bool code = ofSkin ? skin.Source == null && !skin.BuiltIn && skin.SourceAssembly == null : !mount.FromPack;
            Header(ofSkin ? "Sounds of this skin  ([Sounds]" + (ride ? "" : ", [Sounds.Cart]") + ")" : "Sounds of the mount  (mount.cfg [Sounds]" + (ride ? "" : ", [Mount.Sounds.Cart]") + ", shared by its skins)");
            if (ofSkin)
            {
                var missing = new List<string>();
                foreach (var c in Sounds.Coverage(skin, mount)) if (c.From == "missing") missing.Add(c.Event);
                GUILayout.Label(missing.Count > 0
                    ? "MISSING (the donkey's own sounds, silent on this mount until this skin or its mount gives one): " + string.Join(", ", missing) + "."
                    : mount == Catalog.Donkey ? "The donkey: every event has its sound (the player's keys)." : "Nothing missing: every one of the donkey's own sounds has one here (the log at the game's start lists every skin).",
                    missing.Count > 0 ? changed : small);
            }
            if (code)
            {
                GUILayout.Label("From code: its sounds are the mod's own (MountSound) and are not edited here.", small);
                return;
            }
            GUILayout.Label("Per event: files of the pack's sounds folder (a.ogg, b.ogg), a game sound id (id#n for one clip), none, or ground (a step by the ground). Empty: not said, the layer below applies. The volume row writes <event>.volume. What plays on the ridden mount, and from which layer, stands to the right.", small);
            Mount.Instance ridden = Mount.Ridden;
            foreach (SoundEvents.Def d in SoundEvents.All)
            {
                string key = d.Id;
                string text = ofSkin ? SkinSoundText(skin, ride, key) : MountSoundText(mount, ride, key);
                string editKey = (ofSkin ? "s:" : "m:") + skin.Id + ":" + key;
                if (!soundEdits.TryGetValue(editKey, out string shown)) shown = text ?? "";
                GUILayout.BeginHorizontal();
                GUILayout.Label(Tip(key, d.Description), text != null ? changed : label, Wd(90f));
                string typed = GUILayout.TextField(shown, Wd(220f));
                if (typed != shown) soundEdits[editKey] = typed;
                bool commit = typed != (text ?? "") && (GUILayout.Button(Tip("Set", "Writes this line into the block (saved to the skin.cfg by the auto-save)."), Wd(40f)) || Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return);
                if (commit)
                {
                    soundEdits.Remove(editKey);
                    if (ofSkin) SkinSoundSet(skin, mount, ride, key, typed.Trim()); else MountSoundSet(mount, ride, key, typed.Trim());
                }
                if (text != null && GUILayout.Button(Tip("x", "Takes the line out: the layer below applies again."), Wd(24f)))
                {
                    soundEdits.Remove(editKey);
                    if (ofSkin) SkinSoundSet(skin, mount, ride, key, null); else MountSoundSet(mount, ride, key, null);
                }
                SoundSpec sp = ridden != null && ridden.SoundsRig != null ? ridden.SoundsRig.Resolve(key) : null;
                bool playing = ridden != null && ridden.SoundsRig != null && ridden.SoundsRig.IsPlaying(key, out _);
                GUILayout.Label(sp == null ? (ridden == null ? "" : "nothing") : Colour(sp.Describe(), KindHex[SoundKind(sp)]) + (playing ? Colour("   playing", PlayingHex, bold: true) : ""), RichSmall);
                GUILayout.EndHorizontal();
                string volKey = key + ".volume";
                string volText = ofSkin ? SkinSoundText(skin, ride, volKey) : MountSoundText(mount, ride, volKey);
                float vol = volText != null && float.TryParse(volText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float pv) ? pv : 1f;
                GUILayout.BeginHorizontal();
                GUILayout.Label(Tip("    volume", "x the player's volume for this event (" + volKey + " in the block). Right-click: not said."), volText != null ? changed : label, Wd(90f));
                float nv = GUILayout.HorizontalSlider(vol, 0f, 3f, Wd(220f));
                Rect r = GUILayoutUtility.GetLastRect();
                bool unset = GUI.enabled && Event.current.type == EventType.MouseDown && Event.current.button == 1 && r.Contains(Event.current.mousePosition);
                GUILayout.Label(vol.ToString("0.00"), label, Wd(50f));
                GUILayout.EndHorizontal();
                if (unset)
                {
                    Event.current.Use();
                    if (volText != null) { if (ofSkin) SkinSoundSet(skin, mount, ride, volKey, null); else MountSoundSet(mount, ride, volKey, null); }
                }
                else if (Mathf.Abs(nv - vol) > 0.0001f)
                {
                    string nvText = ((float)Math.Round(nv, 2)).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                    if (ofSkin) SkinSoundSet(skin, mount, ride, volKey, nvText); else MountSoundSet(mount, ride, volKey, nvText);
                }
            }
        }

        private static string SkinSoundText(Looks.SkinDef skin, bool ride, string key)
        {
            Dictionary<string, string> mine = ride ? skin.SoundsRide : skin.SoundsCart;
            if (mine != null && mine.TryGetValue(key, out string v)) return v;
            if (skin.Sounds != null && skin.Sounds.TryGetValue(key, out v)) return v;
            return null;
        }

        private static string MountSoundText(Catalog.Entry mount, bool ride, string key)
        {
            Dictionary<string, object> mine = ride ? mount.SoundsRide : mount.SoundsCart;
            if (mine != null && mine.TryGetValue(key, out object v)) return v as string;
            if (mount.Sounds != null && mount.Sounds.TryGetValue(key, out v)) return v as string;
            return null;
        }

        // A line already in the style's section is changed there; else [Sounds] (both styles). Null takes it out
        // (the style's first, then the shared one).
        private static void SkinSoundSet(Looks.SkinDef skin, Catalog.Entry mount, bool ride, string key, string value)
        {
            Dictionary<string, string> mine = ride ? skin.SoundsRide : skin.SoundsCart;
            if (value == null)
            {
                if (mine != null && mine.Remove(key)) { Catalog.Touch(mount); return; }
                skin.Sounds?.Remove(key);
                Catalog.Touch(mount);
                return;
            }
            if (mine != null && mine.ContainsKey(key)) mine[key] = value;
            else (skin.Sounds ?? (skin.Sounds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)))[key] = value;
            Catalog.Touch(mount);
        }

        private static void MountSoundSet(Catalog.Entry mount, bool ride, string key, string value)
        {
            Dictionary<string, object> mine = ride ? mount.SoundsRide : mount.SoundsCart;
            if (value == null)
            {
                if (mine != null && mine.Remove(key)) { Catalog.Touch(mount); return; }
                mount.Sounds?.Remove(key);
                Catalog.Touch(mount);
                return;
            }
            if (mine != null && mine.ContainsKey(key)) mine[key] = value;
            else (mount.Sounds ?? (mount.Sounds = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)))[key] = value;
            Catalog.Touch(mount);
        }

        // Where the rider sits on this skin: a row of its picture (every player, whatever their AutoSeat), this
        // frame's picture top (Gerry), or none (the player's own seat keys).
        private static void DrawSkinSeat(Looks.SkinDef skin, Catalog.Entry mount)
        {
            Header("The rider's seat on this skin  (seat = <row> | picture)");
            GUILayout.BeginHorizontal();
            bool declared = skin.SeatFromPicture || skin.SeatRow >= 0;
            bool seatFromDefault = skin.Parent != null && !skin.Has(Looks.SkinGiven.Seat);
            GUILayout.Label(Tip("Seat: the picture row the rider's hip sits on" + (seatFromDefault ? "  (from the default)" : ""), "The canvas row from the top (0 = the top edge). The donkey's own seat is row 52 of the 96 canvas. Declared, the rider sits there for every player and their AutoSeat / torso height do nothing on this skin; SeatAdjustPx nudges from it. Right-click: none (the player's own seat); on a skin built on its default, the default's seat."), declared && !seatFromDefault ? changed : label, Wd(LabelW));
            if (skin.SeatFromPicture)
            {
                GUILayout.Label("picture: this frame's own top at the pivot column", label, Wd(SliderW - 84f));
            }
            else
            {
                float shownRow = skin.SeatRow >= 0 ? skin.SeatRow : skin.CanvasH / 2f;
                float v = GUILayout.HorizontalSlider(shownRow, 0f, skin.CanvasH - 1, Wd(SliderW - 84f));
                Rect r = GUILayoutUtility.GetLastRect();
                bool unset = GUI.enabled && Event.current.type == EventType.MouseDown && Event.current.button == 1 && r.Contains(Event.current.mousePosition);
                if (unset)
                {
                    Event.current.Use();
                    if (skin.Parent != null) { if (skin.Has(Looks.SkinGiven.Seat)) { skin.Given &= ~Looks.SkinGiven.Seat; Catalog.Touch(mount); } } // the default's seat again
                    else if (skin.SeatRow >= 0) { skin.SeatRow = -1; skin.Given &= ~Looks.SkinGiven.Seat; Catalog.Touch(mount); }
                }
                else
                {
                    int row = Mathf.RoundToInt(v);
                    if (v != Mathf.Clamp(shownRow, 0f, skin.CanvasH - 1) && (skin.SeatRow < 0 || row != skin.SeatRow)) { skin.SeatRow = row; skin.Given |= Looks.SkinGiven.Seat; Catalog.Touch(mount); }
                }
            }
            GUILayout.Label(skin.SeatFromPicture ? "picture" : skin.SeatRow >= 0 ? skin.SeatRow.ToString() : "none", label, Wd(50f));
            if (GUILayout.Button(Tip(skin.SeatFromPicture ? "Use a row" : "Use the picture", skin.SeatFromPicture ? "A fixed row of the canvas instead of this frame's picture top." : "This frame's own picture top at the pivot column (a game creature that hops)."), Wd(100f)))
            {
                if (skin.SeatFromPicture) { skin.SeatFromPicture = false; skin.SeatRow = skin.CanvasH / 2; }
                else { skin.SeatFromPicture = true; skin.SeatRow = -1; }
                skin.Given |= Looks.SkinGiven.Seat;
                Catalog.Touch(mount);
            }
            GUILayout.EndHorizontal();
            if (!declared) GUILayout.Label("No seat declared: the rider's height is the player's own (AutoSeat, torso height), which differs from player to player. A shipped skin declares its row.", small);
        }

        // The skin's own geometry per style ([Tuning] both styles, [Tuning.Ride] / [Tuning.Cart] one): the same rows the
        // Riding tab has, plain with the player's value when not set, yellow when the skin sets them; right-click = not set.
        private static void DrawSkinTuning(Looks.SkinDef skin, Catalog.Entry mount, bool ride)
        {
            bool seatDeclared = skin.SeatFromPicture || skin.SeatRow >= 0;
            Header("Rider on this skin: " + (ride ? "riding" : "with the cart") + "  (" + (ride ? "[Tuning.Ride]" : "[Tuning.Cart]") + " over [Tuning])");
            GUILayout.BeginHorizontal();
            GUILayout.Label("A value already in [Tuning] (both styles) is changed there; a new one goes into this style's section. The other style's rows show after a style switch.", small);
            if (Mount.IsMounted && GUILayout.Button(Tip(ride ? "Go to cart" : "Go to riding", "Dismounts and mounts again in the other style, in place, as on the Riding tab."), Wd(SliderW)))
            {
                Mounts.SwitchStyle(ride ? MountStyle.Cart : MountStyle.Ride);
            }
            GUILayout.EndHorizontal();
            foreach (var k in SkinKeys)
            {
                if (k.CartOnly && ride) continue;
                if (seatDeclared && k.Key == "SeatOffset") continue; // dead on a skin with a seat: the row is the seat
                SkinValueRow(skin, mount, ride, k.Key, k.Label, k.Tip, k.Min, k.Max, k.Int);
            }
            var other = new List<string>();
            void More(Dictionary<string, object> d2, string where)
            {
                if (d2 == null) return;
                foreach (var kv in d2) if (Array.FindIndex(SkinKeys, k => k.Key.Equals(kv.Key, StringComparison.OrdinalIgnoreCase)) < 0) other.Add($"{kv.Key} = {kv.Value} ({where})");
            }
            if (skin.Tuning != null) foreach (var kv in skin.Tuning) if (Array.FindIndex(SkinKeys, k => k.Key.Equals(kv.Key, StringComparison.OrdinalIgnoreCase)) < 0) other.Add($"{kv.Key} = {kv.Value} (both styles)");
            More(skin.TuningRide, "riding");
            More(skin.TuningCart, "cart");
            if (other.Count > 0) GUILayout.Label("Also in this skin's file (not a row of this panel; the game refuses a key outside Looks.SkinTuningKeys): " + string.Join("; ", other) + ".", small);
        }

        // What a skin row shows: the style's value, else the both-styles value, else the player's own (plain, not set).
        private static bool SkinHas(Looks.SkinDef skin, bool ride, string key, out object value)
        {
            Dictionary<string, object> mine = ride ? skin.TuningRide : skin.TuningCart;
            if (mine != null && mine.TryGetValue(key, out value)) return true;
            if (skin.TuningValues != null && skin.TuningValues.TryGetValue(key, out value)) return true;
            if (skin.Tuning != null && skin.Tuning.TryGetValue(key, out string text)) { value = text; return true; }
            value = null;
            return false;
        }

        // A value already in [Tuning] (both styles) is changed there; else it goes into this style's section.
        private static void SkinSet(Looks.SkinDef skin, Catalog.Entry mount, bool ride, string key, object value)
        {
            Dictionary<string, object> mine = ride ? skin.TuningRide : skin.TuningCart;
            bool inStyle = mine != null && mine.ContainsKey(key);
            if (!inStyle && skin.Tuning != null && skin.Tuning.ContainsKey(key))
            {
                skin.Tuning[key] = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
            }
            else
            {
                if (mine == null)
                {
                    mine = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    if (ride) skin.TuningRide = mine; else skin.TuningCart = mine;
                }
                mine[key] = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
            }
            Catalog.Touch(mount);
        }

        // Right-click: the style's own value goes first (the shared [Tuning] value shows again); a second right-click
        // takes the shared one out too.
        private static void SkinUnset(Looks.SkinDef skin, Catalog.Entry mount, bool ride, string key)
        {
            Dictionary<string, object> mine = ride ? skin.TuningRide : skin.TuningCart;
            if (mine != null && mine.Remove(key)) { Catalog.Touch(mount); return; }
            skin.Tuning?.Remove(key);
            skin.TuningValues?.Remove(key);
            Catalog.Touch(mount);
        }

        private static void SkinValueRow(Looks.SkinDef skin, Catalog.Entry mount, bool ride, string key, string name, string tip, float min, float max, bool isInt)
        {
            bool set = SkinHas(skin, ride, key, out object raw);
            bool fromDefault = !set && skin.Parent != null && SkinHas(skin.Parent, ride, key, out raw); // built on its default: the default's value until it sets its own
            float value = set || fromDefault ? Convert.ToSingle(raw, System.Globalization.CultureInfo.InvariantCulture) : PlayerValue(ride, key);
            GUILayout.BeginHorizontal();
            GUILayout.Label(Tip(name + (fromDefault ? "  (from the default)" : ""), tip + (set ? "" : fromDefault ? " Not set for the skin: its default's value is shown. Drag to set its own." : " Not set for the skin: your own value is shown. Drag to set it.")), set ? changed : label, Wd(LabelW));
            float v = GUILayout.HorizontalSlider(value, min, max, Wd(SliderW - 84f));
            Rect r = GUILayoutUtility.GetLastRect();
            bool unset = GUI.enabled && Event.current.type == EventType.MouseDown && Event.current.button == 1 && r.Contains(Event.current.mousePosition);
            GUILayout.Label(isInt ? Mathf.RoundToInt(value).ToString() : value.ToString("0.00"), label, Wd(50f));
            GUILayout.EndHorizontal();
            if (unset)
            {
                Event.current.Use();
                if (set) SkinUnset(skin, mount, ride, key);
                return;
            }
            bool touched = v != Mathf.Clamp(value, min, max);
            if (!touched) return;
            if (isInt)
            {
                int iv = Mathf.RoundToInt(v);
                if (!set || iv != Mathf.RoundToInt(value)) SkinSet(skin, mount, ride, key, iv);
            }
            else
            {
                v = (float)Math.Round(v, 2);
                if (!set || Mathf.Abs(v - value) > 0.0001f) SkinSet(skin, mount, ride, key, v);
            }
        }

        private static void DrawSkinOffsets(Looks.SkinDef skin, Catalog.Entry mount)
        {
            string name = string.IsNullOrEmpty(skin.Name) ? skin.Id : skin.Name;
            Header("This skin: " + name + "  (" + skin.Id + ")");
            GUILayout.Label($"{(skin.BuiltIn ? "Built in" : skin.Source != null ? "A pack: " + skin.Source : skin.SourceAssembly != null ? "Embedded in " + skin.SourceAssembly.GetName().Name + ".dll" : "From code by " + skin.Owner)}; {skin.CanvasW}x{skin.CanvasH}{(skin.Tuning != null && skin.Tuning.Count > 0 ? $"; its own [Tuning]: {string.Join(", ", skin.Tuning.Keys)} (the rows below)" : "")}.", small);
            if (skin.Parent != null)
            {
                (int own, int from) = Looks.PictureCounts(skin);
                string parentName = string.IsNullOrEmpty(skin.Parent.Name) ? skin.Parent.Id : skin.Parent.Name;
                GUILayout.Label($"Built on {parentName} ({skin.Parent.Id}): {own} own pictures, {from} from it{(skin.OverDefault ? ", drawn over it" : "")}. A row this skin doesn't set shows the default's value (\"from the default\"); right-click a row it sets to take the default's again.", small);
            }
            else if (Looks.IsVariantSlot(skin))
            {
                GUILayout.Label($"Built on {Looks.SkinKey(skin.MountId, Looks.DefaultSkinName)}, which isn't registered: it stands alone.", small);
            }
            Vector2 ov = skin.OffsetRide;
            Vector2 oc = skin.OffsetCart;
            bool same = ov == oc;
            bool layered = skin.Parent != null, cleared = false, clearedCart = false;
            bool rideFromDefault = layered && !skin.Has(Looks.SkinGiven.Offset), cartFromDefault = layered && !skin.Has(Looks.SkinGiven.OffsetCart) && !skin.Has(Looks.SkinGiven.Offset);
            if (OffsetRow("Pictures along the mount (riding): tail (-) / head (+)", ref ov.x, -32f, 32f, layered, rideFromDefault, ref cleared)) { skin.OffsetRide = ov; if (same) skin.OffsetCart = ov; skin.Given |= Looks.SkinGiven.Offset; Catalog.Touch(mount); }
            if (OffsetRow("Pictures down (-) / up (+) (riding)", ref ov.y, -32f, 32f, layered, rideFromDefault, ref cleared)) { skin.OffsetRide = ov; if (same) skin.OffsetCart = ov; skin.Given |= Looks.SkinGiven.Offset; Catalog.Touch(mount); }
            if (OnOffRow("With the cart: the same offsets", same, same ? label : changed, tip: "Off: the cart's own two rows below (a mount that should stand closer to the shafts)."))
            {
                skin.OffsetCart = same ? ov + new Vector2(1f, 0f) : ov;
                if (same) skin.Given |= Looks.SkinGiven.OffsetCart; else skin.Given &= ~Looks.SkinGiven.OffsetCart; // the cart's own rows, or the ride's again
                Catalog.Touch(mount);
            }
            if (!same)
            {
                if (OffsetRow("Pictures along the mount (cart): tail (-) / head (+)", ref oc.x, -32f, 32f, layered, cartFromDefault, ref clearedCart)) { skin.OffsetCart = oc; skin.Given |= Looks.SkinGiven.OffsetCart; Catalog.Touch(mount); }
                if (OffsetRow("Pictures down (-) / up (+) (cart)", ref oc.y, -32f, 32f, layered, cartFromDefault, ref clearedCart)) { skin.OffsetCart = oc; skin.Given |= Looks.SkinGiven.OffsetCart; Catalog.Touch(mount); }
            }
            // a right-click on a skin built on its default: the default's offsets again (LooksInherit.cs)
            if (cleared) { skin.Given &= ~(Looks.SkinGiven.Offset | Looks.SkinGiven.OffsetCart); Catalog.Touch(mount); }
            if (clearedCart) { skin.Given &= ~Looks.SkinGiven.OffsetCart; Catalog.Touch(mount); }
        }

        // layered: the skin is built on its default (a right-click takes the default's again: cleared, the value left
        // alone); fromDefault: the value shown is the default's.
        private static bool OffsetRow(string name, ref float value, float min, float max, bool layered, bool fromDefault, ref bool cleared)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(Tip(name + (fromDefault ? "  (from the default)" : ""), "Art pixels: this skin's pictures moved on the donkey's own frame (the mount's other skins keep theirs). Right-click: " + (layered ? "the default's again." : "0.")), value != 0f && !fromDefault ? changed : label, Wd(LabelW));
            float v = GUILayout.HorizontalSlider(value, min, max, Wd(SliderW - 84f)); // room for the number and the x
            Rect r = GUILayoutUtility.GetLastRect();
            if (GUI.enabled && Event.current.type == EventType.MouseDown && Event.current.button == 1 && r.Contains(Event.current.mousePosition))
            {
                Event.current.Use();
                if (layered)
                {
                    cleared = true;
                    v = value;
                }
                else v = 0f;
            }
            GUILayout.Label(Mathf.RoundToInt(value).ToString(), label, Wd(50f));
            GUILayout.EndHorizontal();
            int iv = Mathf.RoundToInt(v);
            if (iv != Mathf.RoundToInt(value))
            {
                value = iv;
                return true;
            }
            return false;
        }

        // ---- saving

        private static void DrawMountSaving(Looks.SkinDef skin, Catalog.Entry mount)
        {
            Header("Keeping these values (the skin's file)");
            if (skin == null)
            {
                GUILayout.Label("The donkey's own look has no file: nothing to save here.", small);
                return;
            }
            string path = Looks.CfgPathFor(skin);
            string mountPath = mount != Catalog.Donkey ? Looks.CfgPathFor(mount.Pack) : null; // the mount's rows go into its mount.cfg (MountsSaveIfDue)
            if (path == null)
            {
                GUILayout.Label(skin.BuiltIn || skin.SourceAssembly != null ? "Embedded in a DLL whose sources aren't on this machine (a release build): changes last this session." : "A code skin: its values live in its mod; changes last this session.", small);
            }
            else
            {
                // Saved by itself, like the Cargo tab's layout: a moment after the last change, into the skin's file
                // (the mount's rows into its mount.cfg). Nothing to remember to press.
                GUILayout.Label("Saved by itself a moment after each change: the skin's rows to " + path + (mountPath != null ? " and the mount's rows to " + mountPath : mount != Catalog.Donkey && !mount.FromPack ? "; the mount's rows are the mod's (not saved)" : "") + ". The pictures are untouched; comments in the rewritten sections stay above their header or key, and a remark at the end of a key's line stays on it. A rebuild ships a built-in's files." + (mountsLastSaved != null ? " Last saved " + mountsLastSaved + "." : ""), small);
                GUILayout.BeginHorizontal();
                GUI.enabled = mountsUndo.Count > 0;
                if (GUILayout.Button(Tip($"Undo the last change{(mountsUndo.Count > 0 ? " (" + mountsUndo.Count + ")" : "")}", "Puts the file back as it was before the last saved change and reads it again; again: the one before."), Wd(LabelW)))
                {
                    MountsUndo();
                }
                GUI.enabled = true;
                if (GUILayout.Button(Tip("Reload from the files", "Reads skin.cfg and the mount's mount.cfg again (a change you made outside the game, say)."), Wd(SliderW)))
                {
                    string e = Looks.ReloadCfg(skin);
                    if (e == null && mountPath != null) e = Looks.ReloadMountCfg(mount);
                    Note(e == null ? "Reloaded: the pictures load again (a moment), then the file's values show." : "Not reloaded: " + e);
                    if (e != null) Plugin.Log.LogWarning($"Mounts tab: '{skin.Id}' not reloaded from its file: {e}");
                }
                GUILayout.EndHorizontal();
                if (!string.IsNullOrEmpty(skin.Source))
                {
                    Toggle("Reload by itself when the pack's files change", Plugin.SkinPackAutoReload, null,
                        "Debug.SkinPackAutoReload: a pack in use is read again a second or two after its skin.cfg or a picture is saved (an editor's export, say). Pictures added after the game started need a restart, as for the button.");
                }
            }
            // the skins as the game built them, for the review page (tools/skins/skin_preview_html.py reads game-skins\ first)
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Tip("Export the loaded skins as built", "Writes every skin loaded now (worn by someone at this moment; a skin taken off is unloaded) (its pictures as the game draws them, after the erase compose) and the donkey's own frames to BepInEx/GK2-asset-probe/game-skins/, one PNG per frame. The review page shows those and counts where its own composite would differ. Wear a skin once to have it loaded."), Wd(LabelW)))
            {
                Note(Looks.ExportBuilt(System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "GK2-asset-probe", Looks.ExportDirName)));
            }
            GUILayout.EndHorizontal();
            if (Time.unscaledTime < mountsNoteUntil) GUILayout.Label(mountsNote, small);
        }

        // ---- saving by itself

        private static int mountsSeenVersion = -1;     // Catalog.Version last seen: a change bumps it (Catalog.Touch)
        private static bool mountsDirty;
        private static float mountsLastChange;
        private static string mountsLastSaved;         // "11:42:07 to gerry" for the label
        private static readonly List<(string Label, string Path, string Text, Func<string> Reload)> mountsUndo = new List<(string, string, string, Func<string>)>();
        private static float mountsUndoAt = -100f; // when the last undo step was taken (saves close together share one)

        // Called every frame the panel draws (TuningWindow.Draw): a change on the Mounts tab (Catalog.Touch bumps
        // Catalog.Version) is written 0.75 s after the last one, to the picked skin's file and the mount's declaring files,
        // when the text would differ from what is there. The file's previous text goes on the undo list first.
        private static void MountsSaveIfDue()
        {
            if (Catalog.Version != mountsSeenVersion)
            {
                if (mountsSeenVersion >= 0) { mountsDirty = true; mountsLastChange = Time.unscaledTime; }
                mountsSeenVersion = Catalog.Version;
            }
            if (!mountsDirty || Time.unscaledTime - mountsLastChange < 0.75f) return;
            mountsDirty = false;
            Looks.SkinDef skin = string.IsNullOrEmpty(mountsSkinId) ? null : Looks.Find(mountsSkinId);
            if (skin == null) return;
            // the skin's rows into its skin.cfg, the mount's into its mount.cfg (a pack mount's folder)
            Catalog.Entry mount = skin.Mount;
            var files = new List<(string Label, string Path, Func<(string Error, bool Changed)> Save, Func<string> Reload)>
            {
                ("skin " + skin.Id, Looks.CfgPathFor(skin), () => (Looks.SaveCfg(skin, out bool c1), c1), () => Looks.ReloadCfg(skin)),
            };
            if (mount != Catalog.Donkey && mount.Pack != null)
                files.Add(("mount " + mount.Id, Looks.CfgPathFor(mount.Pack), () => (Looks.SaveMountCfg(mount, out bool c2), c2), () => Looks.ReloadMountCfg(mount)));
            var written = new List<string>();
            var errors = new List<string>();
            foreach (var f in files)
            {
                string path = f.Path;
                if (path == null) continue;
                string before = System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : null;
                (string e, bool changed) = f.Save();
                if (e != null) { errors.Add(f.Label + ": " + e); continue; }
                if (!changed) continue;
                // one undo step per gesture: a save within a few seconds of the last one on the same file keeps the
                // earlier restore point (a slider dragged in two moves with a pause between was two steps, 2026-10-20)
                bool sameGesture = mountsUndo.Count > 0 && mountsUndo[mountsUndo.Count - 1].Path == path && Time.unscaledTime - mountsUndoAt < 4f;
                if (!sameGesture)
                {
                    mountsUndo.Add((f.Label, path, before, f.Reload));
                    if (mountsUndo.Count > 50) mountsUndo.RemoveAt(0);
                }
                mountsUndoAt = Time.unscaledTime;
                written.Add(f.Label);
            }
            if (errors.Count > 0) Note("Not saved: " + string.Join("; ", errors));
            if (written.Count > 0) mountsLastSaved = DateTime.Now.ToString("HH:mm:ss") + " to " + string.Join(", ", written);
        }

        private static void MountsUndo()
        {
            if (mountsUndo.Count == 0) return;
            (string Label, string Path, string Text, Func<string> Reload) last = mountsUndo[mountsUndo.Count - 1];
            mountsUndo.RemoveAt(mountsUndo.Count - 1);
            try
            {
                if (last.Text == null) System.IO.File.Delete(last.Path);
                else System.IO.File.WriteAllText(last.Path, last.Text, new System.Text.UTF8Encoding(false));
                string e = last.Reload();
                mountsSeenVersion = Catalog.Version; // the reload is not a change to save again
                mountsDirty = false;
                Note(e == null ? "Undone: the file is as before that change, and read again." : "Undone in the file, but not reloaded: " + e);
                if (e == null) Plugin.Log.LogInfo($"Mounts tab: undo: {last.Label} put back as before the last save and read again ({mountsUndo.Count} more to undo).");
                else Plugin.Log.LogWarning($"Mounts tab: undo: {last.Label} put back in the file, but not read again: {e}");
            }
            catch (Exception e)
            {
                Note("Couldn't undo: " + e.Message);
                Plugin.Log.LogWarning($"Mounts tab: undo of {last.Label} failed: {e.Message}");
            }
        }

        private static void Note(string text)
        {
            mountsNote = text;
            mountsNoteUntil = Time.unscaledTime + 5f;
        }
    }
}
