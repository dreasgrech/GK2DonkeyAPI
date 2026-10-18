using System;
using System.Collections.Generic;
using GK2DonkeyAPI.Api;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // The Mounts tab (docs/mounts-design.md §6): one mount at a time, what you ride by default. The rows are the
    // mount's own values (its mount kind's: the rider's seat per style, the cart, foals, an effect; its skin's: the
    // pictures' offsets, the foals), kept apart from your settings on the other tabs: a value set here belongs to every
    // donkey that is this mount, in every save, and your own values (Riding tab) are untouched. Yellow = set for
    // the mount; right-click = not set (your value applies). Changes show on the ridden donkey at once; "Save" writes
    // them into the mount's skin.cfg (a pack's own file, a built-in's in the repo on the machine with the sources).
    internal static partial class TuningWindow
    {
        private static string mountsSkinId;           // the mount being tuned (a skin id, "" = the donkey)
        private static bool mountsFollowRide = true;  // follow what is ridden until the user picks one
        private static bool mountsSaveConfirm;
        private static float mountsSaveConfirmUntil;
        private static string mountsNote = "";
        private static float mountsNoteUntil;

        // The seat, leg and cart keys a mount may set (reference.md [Ride] / [Cart]), with the ranges the Riding tab
        // uses. CartOnly: the key means nothing while riding plain, so its row shows for the cart style only.
        private static readonly (string Key, string Label, string Tip, float Min, float Max, bool Int, bool CartOnly)[] MountKeys =
        {
            ("RiderOffsetPx", "Whole rider along the mount: tail (-) / head (+)", "Art pixels. The same key as the Riding tab's; this value replaces yours while the mount is this one.", -16, 16, true, false),
            ("TorsoOffsetPx", "Torso only along the mount: tail (-) / head (+)", "Art pixels; the legs stay put.", -16, 16, true, false),
            ("SeatAdjustPx", "Torso down (-) / up (+)", "Art pixels on top of the seat: the skin's own picture top (seat = picture), the automatic seat (AutoSeat on), else nothing: the torso height row below is the seat then.", -16, 16, true, false),
            ("SeatOffset", "Torso height (AutoSeat off)", "World units above the ground, the seat most players have (AutoSeat is off by default): what the mount's seat should be for its drawing. Ignored on a skin with seat = picture.", 0f, 1.5f, false, false),
            ("LegAdjustXPx", "Legs along the mount: head (-) / tail (+)", "Art pixels, relative to the torso.", -8, 8, true, false),
            ("LegAdjustYPx", "Legs down (-) / up (+)", "Art pixels, relative to the back line.", -8, 8, true, false),
            ("LegHeightPx", "Rows of the body frame that are legs", "How many bottom rows of the keeper's frame are cut as legs (feet to belt).", 0, 30, true, false),
            ("BackSeatExtraPx", "Seen from behind (armour): seat lifted by", "Art pixels on top of the seat when the rider faces up in armour.", 0, 12, true, false),
            ("HitchGapPx", "Cart further behind (+) / closer (-)", "Art pixels. The whole cart, shafts included, sits this much further behind the mount than behind the donkey, so a longer body isn't drawn inside the cart. The cart stays where it was drawn when it is left and taken again.", -32, 64, true, true),
            ("SpeedMultiplier", "Speed (x normal walking)", "How fast this mount goes.", 0.5f, 4f, false, false),
        };

        private static void DrawMounts()
        {
            Looks.SkinDef skin = PickMount();
            Kinds.KindDef kind = skin != null ? skin.Kind : Kinds.Donkey;
            string mount = MountLabel(skin, kind);
            bool ride = PanelStyle == MountStyle.Ride;
            Mount.Instance r = Mount.Ridden;
            bool ridingIt = r != null && string.Equals(r.LooksRig.Wearing, skin?.Id ?? "", StringComparison.OrdinalIgnoreCase);

            Header("Tuning: " + mount);
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
                : r != null ? $"You ride a {MountLabel(Looks.Find(r.LooksRig.Wearing), r.LooksRig.KindNow)}; ride this one to see these changes live (Saddle Up: Donkey, Skin)."
                : "Nothing is ridden: pick this skin in Saddle Up (Donkey, Skin) and ride it to see the changes live.", small);
            if (EditingNow)
            {
                GUILayout.Label($"Note: {editing.Describe()} is being edited on the Instances tab. This tab never changes that one donkey: it changes the mount and skin, and a value that donkey has of its own (magenta there) still wins on it.", small);
            }
            GUILayout.Label("A mount (Donkey, Giant Rat, Dragon) has skins (the game's donkey, Zebra; Red Dragon, Blue Dragon). The mount's rows are shared by all its skins; the skin's rows are that look's only. They belong to the mount and skin, not to you: every donkey that is them uses these values, in every save and for every player who has the file. Your own settings on the other tabs are untouched; a value set here replaces yours only while a donkey is this mount. Yellow = set here; right-click a row = not set (yours applies).", small);

            if (kind == Kinds.Donkey)
            {
                Header("The mount: Donkey (the game's own)");
                GUILayout.Label("The donkey mount has no values of its own: the Riding tab is where it is tuned. Its skins (Zebra, Ghost, ...) can still move their pictures below.", small);
            }
            else
            {
                DrawMountRows(kind, skin, ride);
            }
            if (skin != null)
            {
                DrawSkinRows(skin, kind);
            }
            DrawMountSaving(skin, kind);
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
        private static string MountLabel(Looks.SkinDef skin, Kinds.KindDef kind)
        {
            if (skin == null) return "Donkey (the game's own skin)";
            string name = string.IsNullOrEmpty(skin.Name) ? skin.Id : skin.Name;
            return kind.Label + " (skin: " + name + ")";
        }

        // ---- the mount kind's rows

        private static void DrawMountRows(Kinds.KindDef kind, Looks.SkinDef skin, bool ride)
        {
            var skins = Kinds.SkinsOf(kind);
            var names = new List<string>();
            foreach (Looks.SkinDef s in skins) names.Add(string.IsNullOrEmpty(s.Name) ? s.Id : s.Name);
            Header("The mount: " + kind.Label + (skins.Count > 1 ? "  (shared by " + string.Join(", ", names) + ")" : ""));
            GUILayout.Label(kind.FromPack
                ? "Declared by its skin.cfg ([Mount]). The rows are what the file says, until you save."
                : $"Registered in code by {kind.Owner}{(kind.Abilities.Length > 0 ? $", with {kind.Abilities.Length} abilit{(kind.Abilities.Length == 1 ? "y" : "ies")}" : "")}: changes here last this session (the mod's code is where they live).", small);

            if (OnOffRow("Pulls the cart", kind.Cart, kind.Cart ? label : changed, tip: "Off: the cart style, hitching and the style switch are refused for this mount, by name."))
            {
                kind.Cart = !kind.Cart;
                Kinds.Touch(kind);
            }
            if (OnOffRow("Babies can be it", kind.Foals, kind.Foals ? label : changed, tip: "Off: the babies stay plain donkeys whatever the parent is."))
            {
                kind.Foals = !kind.Foals;
                Kinds.Touch(kind);
            }
            if (kind.Foals)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(Tip("Babies' size (x the grown one)", "The view's scale of a baby: the donkey's foals are 0.60; a mount drawn three times its creature's size (Gerry) wants 0.33 so its babies are the creature's own size. Saddle Up's babies take the new size within a second. Saved as foal_scale in the mount's file."), Mathf.Abs(kind.FoalScale - 0.6f) > 0.001f ? changed : label, Wd(LabelW));
                float size = GUILayout.HorizontalSlider(kind.FoalScale, 0.2f, 1f, Wd(SliderW - 50f));
                GUILayout.Label(kind.FoalScale.ToString("0.00"), label, Wd(50f));
                GUILayout.EndHorizontal();
                size = Mathf.Round(size * 100f) / 100f;
                if (Mathf.Abs(size - kind.FoalScale) > 0.001f)
                {
                    kind.FoalScale = Mathf.Clamp(size, 0.2f, 1f);
                    Kinds.Touch(kind);
                }
            }
            EffectRows(kind);

            Header("Rider on this mount: " + (ride ? "riding" : "with the cart") + "  (" + (ride ? "[Mount.Tuning.Ride]" : "[Mount.Tuning.Cart]") + ")");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Each style has its own values; the other style's rows show after a style switch.", small);
            if (skin != null && skin.SeatFromPicture) GUILayout.Label("This skin says seat = picture: the rider sits on its own picture, this frame's top, and 'Torso down / up' adjusts from there. The torso height row is ignored on it.", small);
            else if (!Plugin.For(ride ? MountStyle.Ride : MountStyle.Cart).AutoSeat.Entry.Value) GUILayout.Label("Your Riding tab has AutoSeat off (the default for everyone): the seat is the torso height, and 'Torso down / up' does nothing. Set the mount's torso height here, or give its skin seat = picture.", small);
            if (Mount.IsMounted && GUILayout.Button(Tip(ride ? "Go to cart" : "Go to riding", "Dismounts and mounts again in the other style, in place, as on the Riding tab."), Wd(SliderW)))
            {
                Mounts.SwitchStyle(ride ? MountStyle.Cart : MountStyle.Ride);
            }
            GUILayout.EndHorizontal();
            Dictionary<string, object> mine = ride ? kind.TuningRide : kind.TuningCart;
            Dictionary<string, object> both = kind.Tuning;
            foreach (var k in MountKeys)
            {
                if (k.CartOnly && ride) continue;
                KindValueRow(kind, ride, k.Key, k.Label, k.Tip, k.Min, k.Max, k.Int);
            }
            KindBoolRow(kind, ride, "NearLeg", "Rider's legs drawn in front of the mount", "His own baked-in legs hidden, a leg overlay drawn in front (the Riding tab's NearLeg).");
            OtherKindKeys(kind, ride);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Tip(ride ? "Copy these to the cart style" : "Copy these to riding", "The rows set for this style become the other style's too."), Wd(LabelW)))
            {
                var from = ride ? kind.TuningRide : kind.TuningCart;
                if (from != null && from.Count > 0)
                {
                    var to = new Dictionary<string, object>(from, StringComparer.OrdinalIgnoreCase);
                    if (ride) kind.TuningCart = to; else kind.TuningRide = to;
                    Kinds.Touch(kind);
                    Note("Copied.");
                }
            }
            if (GUILayout.Button(Tip("Clear this style", "Every row of this style back to not set."), Wd(SliderW)))
            {
                if (ride) kind.TuningRide = null; else kind.TuningCart = null;
                Kinds.Touch(kind);
            }
            GUILayout.EndHorizontal();
        }

        private static void EffectRows(Kinds.KindDef kind)
        {
            IReadOnlyList<MountEffectInfo> effects = Mounts.Effects.Effects;
            var ids = new List<string> { "" };
            var labels = new List<string> { "none" };
            foreach (MountEffectInfo e in effects)
            {
                ids.Add(e.Id);
                labels.Add(e.Name);
            }
            int idx = Math.Max(0, ids.IndexOf(kind.Effect ?? ""));
            GUILayout.BeginHorizontal();
            GUILayout.Label(Tip("Comes with the effect", "Shown on a donkey of this kind whose own Effect is 'As the mount' (the default row in Saddle Up)."), idx > 0 ? changed : label, Wd(LabelW));
            if (GUILayout.Button("<", Wd(22f))) { idx = (idx - 1 + ids.Count) % ids.Count; kind.Effect = ids[idx].Length > 0 ? ids[idx] : null; Kinds.Touch(kind); }
            GUILayout.Label(labels[idx], label, Wd(SliderW - 44f));
            if (GUILayout.Button(">", Wd(22f))) { idx = (idx + 1) % ids.Count; kind.Effect = ids[idx].Length > 0 ? ids[idx] : null; Kinds.Touch(kind); }
            GUILayout.EndHorizontal();
            if (idx > 0)
            {
                MountEffectWhen[] whens = { MountEffectWhen.Off, MountEffectWhen.Night, MountEffectWhen.Always };
                string[] whenNames = { "Never", "At night", "Always" };
                int w = Math.Max(0, Array.IndexOf(whens, kind.EffectWhen));
                GUILayout.BeginHorizontal();
                GUILayout.Label("Shown", label, Wd(LabelW));
                if (GUILayout.Button("<", Wd(22f))) { w = (w - 1 + whens.Length) % whens.Length; kind.EffectWhen = whens[w]; Kinds.Touch(kind); }
                GUILayout.Label(whenNames[w], label, Wd(SliderW - 44f));
                if (GUILayout.Button(">", Wd(22f))) { w = (w + 1) % whens.Length; kind.EffectWhen = whens[w]; Kinds.Touch(kind); }
                GUILayout.EndHorizontal();
            }
        }

        // What a row shows: the style's value, else the both-styles value, else the player's own (plain, not set).
        private static bool KindHas(Kinds.KindDef kind, bool ride, string key, out object value)
        {
            Dictionary<string, object> mine = ride ? kind.TuningRide : kind.TuningCart;
            if (mine != null && mine.TryGetValue(key, out value)) return true;
            if (kind.Tuning != null && kind.Tuning.TryGetValue(key, out value)) return true;
            value = null;
            return false;
        }

        private static void KindSet(Kinds.KindDef kind, bool ride, string key, object value)
        {
            Dictionary<string, object> mine = ride ? kind.TuningRide : kind.TuningCart;
            if (mine == null)
            {
                mine = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                if (ride) kind.TuningRide = mine; else kind.TuningCart = mine;
            }
            mine[key] = value;
            Kinds.Touch(kind);
        }

        private static void KindUnset(Kinds.KindDef kind, bool ride, string key)
        {
            (ride ? kind.TuningRide : kind.TuningCart)?.Remove(key);
            kind.Tuning?.Remove(key);
            Kinds.Touch(kind);
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

        private static void KindValueRow(Kinds.KindDef kind, bool ride, string key, string name, string tip, float min, float max, bool isInt)
        {
            bool set = KindHas(kind, ride, key, out object raw);
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
                if (set) KindUnset(kind, ride, key);
                return;
            }
            // only a drag is a change: the slider hands back the value it was given (clamped) when it isn't touched
            bool touched = v != Mathf.Clamp(value, min, max);
            if (!touched) return;
            if (isInt)
            {
                int iv = Mathf.RoundToInt(v);
                if (!set || iv != Mathf.RoundToInt(value)) KindSet(kind, ride, key, iv);
            }
            else
            {
                v = (float)Math.Round(v, 2);
                if (!set || Mathf.Abs(v - value) > 0.0001f) KindSet(kind, ride, key, v);
            }
        }

        // Keys a mount sets that the rows above don't show (any tuning key may be set in its file or by code): listed,
        // so nothing a mount does is hidden here.
        private static void OtherKindKeys(Kinds.KindDef kind, bool ride)
        {
            var shown = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "NearLeg" };
            foreach (var k in MountKeys) shown.Add(k.Key);
            var parts = new List<string>();
            void Add(Dictionary<string, object> d, string where)
            {
                if (d == null) return;
                foreach (var kv in d) if (!shown.Contains(kv.Key)) parts.Add($"{kv.Key} = {kv.Value} ({where})");
            }
            Add(kind.Tuning, "both styles");
            Add(kind.TuningRide, "riding");
            Add(kind.TuningCart, "cart");
            if (parts.Count > 0) GUILayout.Label("Also set for this mount (its file or code; not editable here): " + string.Join("; ", parts) + ".", small);
        }

        private static void KindBoolRow(Kinds.KindDef kind, bool ride, string key, string name, string tip)
        {
            bool set = KindHas(kind, ride, key, out object raw);
            bool value = set ? Convert.ToBoolean(raw) : PlayerValue(ride, key) > 0.5f;
            GUILayout.BeginHorizontal();
            GUILayout.Label(Tip(name, tip + (set ? "" : " Not set for the mount: your own value is shown.")), set ? changed : label, Wd(LabelW));
            if (GUILayout.Button(value ? "On" : "Off", Wd(70f))) KindSet(kind, ride, key, !value);
            if (set && GUILayout.Button(Tip("x", "Back to not set: your value applies (the sliders above do the same on a right-click)."), Wd(24f))) KindUnset(kind, ride, key);
            GUILayout.EndHorizontal();
        }

        // ---- the skin's rows

        private static void DrawSkinRows(Looks.SkinDef skin, Kinds.KindDef kind)
        {
            string name = string.IsNullOrEmpty(skin.Name) ? skin.Id : skin.Name;
            Header("This skin: " + name + "  (" + skin.Id + ")");
            GUILayout.Label($"{(skin.BuiltIn ? "Built in" : skin.Source != null ? "A pack: " + skin.Source : "From code by " + skin.Owner)}; {skin.CanvasW}x{skin.CanvasH}{(skin.Tuning != null && skin.Tuning.Count > 0 ? $"; its own [Tuning]: {string.Join(", ", skin.Tuning.Keys)} (edit in the file)" : "")}.", small);
            Vector2 ov = skin.OffsetRide;
            Vector2 oc = skin.OffsetCart;
            bool same = ov == oc;
            if (OffsetRow("Pictures along the mount (riding): tail (-) / head (+)", ref ov.x, -32f, 32f)) { skin.OffsetRide = ov; if (same) skin.OffsetCart = ov; Kinds.Touch(kind); }
            if (OffsetRow("Pictures down (-) / up (+) (riding)", ref ov.y, -32f, 32f)) { skin.OffsetRide = ov; if (same) skin.OffsetCart = ov; Kinds.Touch(kind); }
            if (OnOffRow("With the cart: the same offsets", same, same ? label : changed, tip: "Off: the cart's own two rows below (a mount that should stand closer to the shafts)."))
            {
                skin.OffsetCart = same ? ov + new Vector2(1f, 0f) : ov;
                Kinds.Touch(kind);
            }
            if (!same)
            {
                if (OffsetRow("Pictures along the mount (cart): tail (-) / head (+)", ref oc.x, -32f, 32f)) { skin.OffsetCart = oc; Kinds.Touch(kind); }
                if (OffsetRow("Pictures down (-) / up (+) (cart)", ref oc.y, -32f, 32f)) { skin.OffsetCart = oc; Kinds.Touch(kind); }
            }
        }

        private static bool OffsetRow(string name, ref float value, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(Tip(name, "Art pixels: this skin's pictures moved on the donkey's own frame (the mount's other skins keep theirs). Right-click: 0."), value != 0f ? changed : label, Wd(LabelW));
            float v = GUILayout.HorizontalSlider(value, min, max, Wd(SliderW - 84f)); // room for the number and the x
            Rect r = GUILayoutUtility.GetLastRect();
            if (GUI.enabled && Event.current.type == EventType.MouseDown && Event.current.button == 1 && r.Contains(Event.current.mousePosition))
            {
                Event.current.Use();
                v = 0f;
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

        private static void DrawMountSaving(Looks.SkinDef skin, Kinds.KindDef kind)
        {
            Header("Keeping these values (the skin's file)");
            if (skin == null)
            {
                GUILayout.Label("The donkey's own look has no file: nothing to save here.", small);
                return;
            }
            string path = Looks.CfgPathFor(skin);
            var kindFiles = new List<Looks.SkinDef>();
            if (kind != Kinds.Donkey && kind.FromPack)
            {
                foreach (Looks.SkinDef s in Kinds.SkinsOf(kind))
                {
                    string p = Looks.CfgPathFor(s);
                    if (p != null && System.IO.File.Exists(p) && System.IO.File.ReadAllText(p).IndexOf("[Mount]", StringComparison.OrdinalIgnoreCase) >= 0) kindFiles.Add(s);
                }
            }
            if (path == null)
            {
                GUILayout.Label(skin.BuiltIn ? "Built in, and the mod's sources aren't on this machine: changes last this session." : "A code skin: its values live in its mod; changes last this session.", small);
            }
            else
            {
                GUILayout.Label("Saves the skin's rows to " + path + (kindFiles.Count > 0 ? " and the mount's rows to the file(s) that declare the mount: " + string.Join(", ", kindFiles.ConvertAll(s => s.Id)) : kind != Kinds.Donkey && !kind.FromPack ? "; the mount's rows are the mod's (not saved)" : "") + ". The pictures are untouched; comments inside the rewritten sections ([Tuning], [Mount...]) and on the rewritten keys are not kept. A rebuild ships a built-in's file.", small);
                GUILayout.BeginHorizontal();
                bool confirming = mountsSaveConfirm && Time.unscaledTime < mountsSaveConfirmUntil;
                if (GUILayout.Button(Tip(confirming ? "Click again to save" : "Save to the skin's file", "Writes the values above into skin.cfg (the mount's rows into every file that declares the mount); asks once more first."), Wd(LabelW)))
                {
                    if (!confirming)
                    {
                        mountsSaveConfirm = true;
                        mountsSaveConfirmUntil = Time.unscaledTime + 4f;
                    }
                    else
                    {
                        mountsSaveConfirm = false;
                        var errors = new List<string>();
                        string e = Looks.SaveCfg(skin);
                        if (e != null) errors.Add(skin.Id + ": " + e);
                        foreach (Looks.SkinDef s in kindFiles)
                        {
                            if (s == skin) continue;
                            e = Looks.SaveCfg(s);
                            if (e != null) errors.Add(s.Id + ": " + e);
                        }
                        Note(errors.Count == 0 ? "Saved." : "Not saved: " + string.Join("; ", errors));
                    }
                }
                if (GUILayout.Button(Tip("Reload from the file", "Reads skin.cfg again and throws away unsaved changes here."), Wd(SliderW)))
                {
                    string e = Looks.ReloadCfg(skin);
                    Note(e == null ? "Reloaded: the pictures load again (a moment), then the file's values show." : "Not reloaded: " + e);
                }
                GUILayout.EndHorizontal();
            }
            if (Time.unscaledTime < mountsNoteUntil) GUILayout.Label(mountsNote, small);
        }

        private static void Note(string text)
        {
            mountsNote = text;
            mountsNoteUntil = Time.unscaledTime + 5f;
        }
    }
}
