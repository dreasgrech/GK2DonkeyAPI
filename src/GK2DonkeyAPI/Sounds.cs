using System;
using System.Collections.Generic;
using HarmonyLib;
using LazyBearTechnology;
using UnityEngine;

using GK2DonkeyAPI.Api;

namespace GK2DonkeyAPI
{
    // Mount sounds (docs/mount-sounds-design.md; the game's side in PROBE-FINDINGS.md Round 12). The game's donkey walks
    // silently and the cart has no sound, so the mod plays every mount sound itself through the game's own player
    // (LazyAudio): game sound ids, or a pack's own clips registered as game sounds (SoundBank), 3D at the mount, in
    // the game's mixer groups, so the in-game volume sliders apply. Every instance has a Rig: the timers and the
    // animator reading (footfalls and wheel turns are points in the walk clip's normalised time, grazing is the
    // graze clip starting) are its own, so a standing donkey, a foal and a herd sound as well as the ridden one.
    // What plays for an event is a SoundSpec from the layers (SoundEvents: the player's cfg keys are the base;
    // a mount's and a skin's blocks and a mod's override come over it). Nothing here touches game data.
    internal static partial class Sounds
    {
        private static readonly RaycastHit[] hits = new RaycastHit[8];
        private static readonly HashSet<string> warnedIds = new HashSet<string>();

        // The last event any rig played, for the dev tab.
        public static string LastEvent { get; private set; } = "";

        internal static bool StandingOn => Plugin.StandingSounds == null || Plugin.StandingSounds.Value;

        // The herd (docs/mount-sounds-design.md 3.3): a standing grown one calls StandingIdleFactor times rarer than the
        // ridden one, and no standing call comes within VoiceGapS of any voice in the scene; babies never call by
        // themselves, they answer their parent.
        internal static float StandingIdleFactor = 3f;
        internal static float VoiceGapS = 4f;
        private static float nextVoiceAt;
        internal static readonly List<(float At, Mount.Instance Who, string Event)> TestVoiceTimes = new List<(float, Mount.Instance, string)>();
        internal static void TestResetHerd() { nextVoiceAt = 0f; TestVoiceTimes.Clear(); }

        // ------------------------------------------------------------------ the ridden instance's shortcuts

        internal static Rig RiddenRig => Mount.Ridden?.SoundsRig;

        public static void Bray() => RiddenRig?.Emit("voice");

        public static void RingBell() => RiddenRig?.Emit("bell");

        // An item landed on a cart: a pickup while riding, the head's load at mount, or loading a standing cart on
        // foot. The cart is the bound instance (Mount.Current: the ridden one, or the standing cart loaded on foot).
        public static void CargoLanded()
        {
            Mount.Instance cart = Mount.Current;
            if (cart == null || !cart.HasCart) return;
            cart.SoundsRig.Emit("cargo");
        }

        // The dev tab's buttons, by their old names: the event on the bound or ridden instance.
        public static void Preview(string what)
        {
            Rig r = (Mount.Current ?? Mount.Ridden)?.SoundsRig;
            if (r == null) return;
            switch (what)
            {
                case "hoof": r.Emit("step"); break;
                case "bray": r.Emit("voice"); break;
                case "thump": r.Emit("cargo"); break;
                default: r.Emit(what); break;
            }
        }

        // ------------------------------------------------------------------ playback

        // Plays a sound id 3D at a transform with the final volume (no setting read). Volume and pitch multiply the
        // game's own values for that sound. An id the game does not have is refused (the game would play its
        // placeholder clip instead) and logged once; the bank's own ids are put back into the game's list first when
        // a config re-bind dropped them. startAt: seconds into the clip to start from (SoundHandler.SetTime right
        // after the play call, before the first audio frame).
        internal static SoundHandler PlayId(string id, Transform at, float volume, float pitch, float startAt = 0f)
        {
            id = id?.Trim(); // a hand-edited or pasted id may carry spaces
            if (string.IsNullOrEmpty(id) || at == null || !LazyAudio.IsInitialized)
            {
                return null;
            }
            if (volume <= 0f)
            {
                return null;
            }
            try
            {
                AudioConfig cfg = LazySingletonSO<AudioConfig>.Instance;
                int clip = SplitClip(ref id);
                SoundBank.Entry own = SoundBank.Find(id);
                if (own != null) SoundBank.Ensure(own);
                Sound sound = cfg?.Get(id);
                if (sound == null || clip > sound.samples.Count)
                {
                    if (warnedIds.Add(id + "#" + clip))
                    {
                        Plugin.Log.LogWarning(sound == null ? $"Sounds: the game has no sound '{id}'; nothing played." : $"Sounds: '{id}' has {sound.samples.Count} clip(s), not {clip}; nothing played.");
                    }
                    return null;
                }
                SoundHandler h = clip > 0 ? PlayClip(sound, clip, id, at) : LazyAudio.PlayAtGameObject(id, at, SpatialType.sound3D, checkDelay: false);
                if (h == null)
                {
                    return null;
                }
                h.SetVolume(volume);
                h.SetPitch(Mathf.Clamp(pitch, 0.1f, 3f));
                if (startAt > 0f)
                {
                    h.SetTime(startAt);
                }
                // The game's pooled players keep the fade times of whatever used them last, and a fade re-applies
                // the volume read at fade setup every frame; zero fades with the volume above as the target.
                h.SetFadeInFadeOut(0f, 0f);
                return h;
            }
            catch (Exception e)
            {
                if (warnedIds.Add("!" + id))
                {
                    Plugin.Log.LogWarning($"Sounds: playing '{id}' failed: {e.Message}");
                }
                return null;
            }
        }

        // "id#n": clip n (1-based) of the sound; 0 for a plain id (the game picks a clip at random).
        private static int SplitClip(ref string id)
        {
            int hash = id.LastIndexOf('#');
            if (hash > 0 && int.TryParse(id.Substring(hash + 1), out int n) && n > 0)
            {
                id = id.Substring(0, hash);
                return n;
            }
            return 0;
        }

        // The game picks the clip inside the play call (LazyAudio.PlaySound -> Sound.RandomSample, which skips disabled
        // samples), so only the wanted clip is enabled for that call and the others are put back right after.
        private static SoundHandler PlayClip(Sound sound, int clip, string id, Transform at)
        {
            var was = new bool[sound.samples.Count];
            try
            {
                for (int k = 0; k < sound.samples.Count; k++)
                {
                    was[k] = sound.samples[k].isEnabled;
                    sound.samples[k].isEnabled = k == clip - 1;
                }
                return LazyAudio.PlayAtGameObject(id, at, SpatialType.sound3D, checkDelay: false);
            }
            finally
            {
                for (int k = 0; k < sound.samples.Count; k++)
                {
                    sound.samples[k].isEnabled = was[k];
                }
            }
        }

        public static bool KnownId(string id)
        {
            try
            {
                AudioConfig cfg = LazySingletonSO<AudioConfig>.Instance;
                id = id?.Trim();
                if (string.IsNullOrEmpty(id) || cfg == null)
                {
                    return false;
                }
                int clip = SplitClip(ref id);
                Sound sound = cfg.Get(id);
                return sound != null && clip <= sound.samples.Count;
            }
            catch
            {
                return false;
            }
        }

        // The game's footstep surface probe (AnimationComponentBase.GetSurfaceType), cast from the donkey's root: the
        // game never runs it for the donkey, and its animation component may sit on an offset child.
        internal static SurfaceType SurfaceUnder(Vector3 pos)
        {
            int n = Physics.RaycastNonAlloc(new Ray(pos + Vector3.up * 0.01666667f, Physics.gravity.normalized), hits, 1f, 2060);
            int best = int.MaxValue;
            SurfaceType type = SurfaceType.None;
            for (int i = 0; i < n; i++)
            {
                IOrderedSurface s = hits[i].collider != null ? hits[i].collider.GetComponent<IOrderedSurface>() : null;
                if (s != null && s.Depth < best)
                {
                    best = s.Depth;
                    type = s.SurfaceType;
                }
            }
            return type;
        }

        internal static string StepIdFor(SurfaceType surface)
        {
            try
            {
                SurfaceStepSoundSettings settings = LazySingletonSO<SurfaceStepSoundSettings>.Instance;
                if (settings != null)
                {
                    string id = settings.GetStepSoundIdForSurfaceType(surface);
                    if (!string.IsNullOrEmpty(id))
                    {
                        return id;
                    }
                }
            }
            catch
            {
            }
            return "step_dirt";
        }

        // ------------------------------------------------------------------ the rig

        // One instance's sounds: its timers, its reading of its animator, what plays for each event. Every entry
        // point binds the owner (Mount.Bind) before reading a setting: Tuned<T>.Value serves an instance's own
        // overrides (a mount's [Mount.Tuning] pins: the snail's BrayVolume = 0) only for the bound instance, and the
        // hitch, the cargo thump, the API and the animation-event prefix are not bound to the emitting instance.
        internal sealed class Rig
        {
            private readonly Mount.Instance owner;
            private readonly List<AnimatorClipInfo> clipInfo = new List<AnimatorClipInfo>(2);
            private AnimationComponentBase boundAnim;
            private AnimationClip lastClip;
            private string lastClipName = "";
            private bool inWalkClip, inGrazeClip, wasWalking;
            private long lastHoofK, lastWheelK;
            private float nextBray = -1f, mountBrayAt = -1f, idleSince;
            private float brayEvery = -1f;  // the interval nextBray was armed with: a layer change (a skin worn, a pack's line) re-arms it
            private float answerAt = -1f;   // a baby: when its answer to the parent's call is due
            private readonly Dictionary<string, SoundHandler> loops = new Dictionary<string, SoundHandler>(StringComparer.Ordinal);
            private readonly Dictionary<string, SoundLine> overrides = new Dictionary<string, SoundLine>(StringComparer.OrdinalIgnoreCase); // a mod's, by event or event.variant (Mounts.Sounds)
            private readonly Dictionary<string, (SoundSpec Spec, float At)> cache = new Dictionary<string, (SoundSpec, float)>(StringComparer.OrdinalIgnoreCase);
            private readonly HashSet<string> warned = new HashSet<string>();
            private bool tickFailedSaid;

            internal string LastEvent { get; private set; } = "";
            // Plays that reached the game's player, by event (the self-tests).
            internal readonly Dictionary<string, int> TestPlays = new Dictionary<string, int>(StringComparer.Ordinal);
            internal int TestTicks;             // TickInner entries (the self-tests: is the rig ticked at all)
            internal string TestLastGate = "";  // why the last tick stopped early, or "ran"
            internal string TestState => $"ticks {TestTicks}, last gate '{TestLastGate}', nextBray {nextBray:0.00} (now {Time.time:0.00}), nextVoiceAt {nextVoiceAt:0.00}, walking last {wasWalking}, baby {IsBaby}, ridden {owner.IsRidden}, standing on {StandingOn}, answerAt {answerAt:0.00}";

            internal Rig(Mount.Instance owner)
            {
                this.owner = owner;
            }

            // A fresh mount (not a re-summon after a door): maybe a call, and count the standing time for the bell.
            internal void OnMounted()
            {
                try
                {
                    using (Mount.Bind(owner))
                    {
                        Reset();
                        float now = Time.time;
                        idleSince = now;
                        SoundSpec voice = Resolve("voice");
                        brayEvery = voice != null ? voice.Number("idle_every", 0f) : 0f;
                        nextBray = now + NextBrayDelay(voice);
                        if (voice != null && May(SoundEvents.Find("voice"), voice) && UnityEngine.Random.value < voice.Number("mount_chance", 0f))
                        {
                            mountBrayAt = now + UnityEngine.Random.Range(0.3f, 0.8f);
                        }
                    }
                }
                catch (Exception e)
                {
                    Warn("mount", "the mount's sound set-up failed: " + e.Message);
                }
            }

            private void Reset()
            {
                StopLoops();
                boundAnim = null;
                inWalkClip = false;
                inGrazeClip = false;
                mountBrayAt = -1f;
                wasWalking = false;
            }

            // The view went away (a dismount's cleanup, a door, the tier's release): nothing left playing, the
            // animator forgotten. The instance keeps its settings; the next view binds anew.
            internal void Detach()
            {
                StopLoops();
                boundAnim = null;
                inWalkClip = false;
                inGrazeClip = false;
            }

            // The instance goes for good.
            internal void Release()
            {
                Detach();
                overrides.Clear();
                cache.Clear();
            }

            // A frame: the ridden instance's from Mount.Tick, a standing one's from StandTick (the Active tier only).
            internal void Tick(bool walking)
            {
                try
                {
                    using (Mount.Bind(owner))
                    {
                        TickInner(walking);
                    }
                }
                catch (Exception e)
                {
                    if (!tickFailedSaid)
                    {
                        tickFailedSaid = true;
                        Plugin.Log.LogWarning($"Sounds: {owner.Describe()}: the sound tick failed: {e.Message}");
                    }
                }
            }

            private void TickInner(bool walking)
            {
                Wgo donkey = owner.Donkey;
                AnimationComponentBase ac = donkey != null && donkey ? donkey.MainWgoPart?.AnimationComponent : null;
                if (ac != boundAnim)
                {
                    StopLoops();
                    boundAnim = ac;
                    inWalkClip = false;
                    inGrazeClip = false;
                }
                float now = Time.time;
                bool ridden = owner.IsRidden;
                if (!walking && wasWalking)
                {
                    idleSince = now;
                }
                bool setOff = walking && !wasWalking;
                float stoodFor = now - idleSince;
                wasWalking = walking;

                StyleSettings S = owner.S;
                TestTicks++;
                if (!S.SoundsOn.Value || !MountRules.Allowed(MountFeature.Sounds) || ac == null || !ridden && !StandingOn)
                {
                    TestLastGate = !S.SoundsOn.Value ? "SoundsOn off" : !MountRules.Allowed(MountFeature.Sounds) ? "the Sounds feature refused" : ac == null ? "no animation component" : "standing with StandingSounds off";
                    StopLoops();
                    return;
                }
                TestLastGate = "ran";

                if (ridden)
                {
                    SoundSpec bell = Resolve("bell");
                    if (setOff && bell != null && bell.Number("on_set_off", 0f) > 0.5f && stoodFor >= bell.Number("set_off_idle", 4f)
                        && (bell.Number("only_with_cargo", 1f) < 0.5f || owner.CargoRig.Count > 0))
                    {
                        Emit("bell");
                    }
                    if (mountBrayAt > 0f && now >= mountBrayAt)
                    {
                        mountBrayAt = -1f;
                        Emit("voice");
                    }
                    SoundSpec voice = Resolve("voice");
                    float every = voice != null ? voice.Number("idle_every", 0f) : 0f;
                    if (every > 0f)
                    {
                        if (nextBray < 0f || Math.Abs(every - brayEvery) > 0.001f)
                        {
                            // armed anew: the first time, and whenever the interval changed (a skin worn, a pack's
                            // line); armed once at the first tick, a donkey's 40 s stayed on a pack that said 1 (2026-10-24)
                            brayEvery = every;
                            nextBray = now + NextBrayDelay(voice);
                        }
                        else if (now >= nextBray)
                        {
                            if (!walking || voice.Number("while_walking", 0f) > 0.5f)
                            {
                                Emit("voice");
                                nextBray = now + NextBrayDelay(voice);
                            }
                            else
                            {
                                nextBray = now + UnityEngine.Random.Range(2f, 6f); // not mid-stride; soon after stopping
                            }
                        }
                    }
                    else
                    {
                        nextBray = -1f;
                    }
                }
                else if (!IsBaby)
                {
                    // a grown one standing: the idle call, rarer than ridden (StandingIdleFactor), never mid-step, and
                    // one voice at a time in the scene (VoiceGapS: a yard of them is no racket); babies only answer
                    SoundSpec voice = Resolve("voice");
                    float every = voice != null ? voice.Number("idle_every", 0f) * StandingIdleFactor : 0f;
                    if (every > 0f)
                    {
                        if (nextBray < 0f || Math.Abs(every - brayEvery) > 0.001f)
                        {
                            brayEvery = every;
                            nextBray = now + NextBrayDelay(voice, StandingIdleFactor);
                        }
                        else if (now >= nextBray)
                        {
                            if (!walking && now >= nextVoiceAt)
                            {
                                Emit("voice");
                                nextBray = now + NextBrayDelay(voice, StandingIdleFactor);
                            }
                            else
                            {
                                nextBray = now + UnityEngine.Random.Range(2f, 6f);
                            }
                        }
                    }
                    else
                    {
                        nextBray = -1f;
                    }
                }
                if (answerAt > 0f && now >= answerAt)
                {
                    answerAt = -1f;
                    Emit("answer");
                }

                Animator a = ac.Animator;
                if (a == null || !a.isActiveAndEnabled)
                {
                    return;
                }
                string clip = "";
                a.GetCurrentAnimatorClipInfo(0, clipInfo);
                if (clipInfo.Count > 0 && clipInfo[0].clip != null)
                {
                    // A clip's name is a new string on every read: kept for the clip it was read from.
                    AnimationClip c = clipInfo[0].clip;
                    if (!ReferenceEquals(c, lastClip))
                    {
                        lastClip = c;
                        lastClipName = c.name;
                    }
                    clip = lastClipName;
                }
                float t = a.GetCurrentAnimatorStateInfo(0).normalizedTime;

                bool walkClip = clip.StartsWith("walk", StringComparison.Ordinal);
                if (walkClip)
                {
                    SoundSpec step = Resolve("step");
                    int n = Mathf.Max(1, Mathf.RoundToInt(step != null ? step.Number("per_cycle", 4f) : 4f));
                    long hk = (long)Math.Floor(t * n + (step != null ? step.Number("phase", 0f) : 0f));
                    SoundSpec wheel = Resolve("wheel");
                    int every = Mathf.Max(1, Mathf.RoundToInt(wheel != null ? wheel.Number("every_cycles", 1f) : 1f));
                    long wk = (long)Math.Floor(t / every + (wheel != null ? wheel.Number("phase", 0f) : 0f));
                    if (!inWalkClip)
                    {
                        // Setting off: the first footfall and wheel turn sound at once. A left/right clip change while
                        // walking keeps counting from the new clip without an extra step.
                        lastHoofK = hk - 1;
                        lastWheelK = wk - 1;
                        inWalkClip = true;
                    }
                    // A step on any change of the step index, in either direction: the walk clip runs backwards while the
                    // keeper backs away in a focus stance (docs/mounted-combat.md 9.2), and counting forwards only left the
                    // hooves silent then. A jump of more than one step (the clip restarted or turned round) resets without a
                    // burst of steps.
                    if (Math.Abs(hk - lastHoofK) > 1)
                    {
                        lastHoofK = hk;
                    }
                    if (hk != lastHoofK)
                    {
                        lastHoofK = hk;
                        Emit("step");
                    }
                    if (Math.Abs(wk - lastWheelK) > 1)
                    {
                        lastWheelK = wk;
                    }
                    if (wk != lastWheelK)
                    {
                        lastWheelK = wk;
                        if (owner.CurrentStyle == MountStyle.Cart)
                        {
                            float pitchScale = 1f;
                            if (wheel != null && wheel.Number("follows_speed", 0f) > 0.5f)
                            {
                                try { pitchScale = Mathf.Clamp(Mathf.Abs(a.GetFloat("walk_speed")), 0.5f, 2.5f); } catch { } // Abs: negative while the walk runs backwards
                            }
                            Emit("wheel", pitchScale: pitchScale);
                        }
                    }
                }
                else
                {
                    inWalkClip = false;
                }

                bool grazeClip = clip.StartsWith("graze", StringComparison.Ordinal);
                if (grazeClip && !inGrazeClip)
                {
                    Emit("graze");
                }
                else if (!grazeClip && inGrazeClip)
                {
                    Stop("graze"); // cut short (the donkey set off): the chewing stops with the animation
                }
                inGrazeClip = grazeClip;
            }

            private static float NextBrayDelay(SoundSpec voice, float factor = 1f)
            {
                float every = voice != null ? voice.Number("idle_every", 0f) * factor : 0f;
                return every <= 0f ? 1e6f : every * UnityEngine.Random.Range(0.5f, 1.5f);
            }

            // The parent called: its babies answer one after another, half a second on, each a third of a second after
            // the one before (docs/foal-sounds.md, calls made in code).
            private void ScheduleAnswers()
            {
                try
                {
                    List<Mount.Instance> kids = owner.Children();
                    if (kids == null) return;
                    int k = 0;
                    foreach (Mount.Instance kid in kids)
                    {
                        if (kid == null || kid.SoundsRig == null || !kid.SoundsRig.IsBaby) continue;
                        kid.SoundsRig.answerAt = Time.time + 0.5f + 0.35f * k + UnityEngine.Random.Range(0f, 0.2f);
                        k++;
                    }
                }
                catch (Exception e)
                {
                    Warn("answers", "the babies' answers could not be set up: " + e.Message);
                }
            }

            // ------------------------------------------------------------------ resolving

            // What plays for an event (and a variant: "grass" for step) on this instance now: a mod's override, then
            // (step 3) the skin's and the mount's blocks, then the base: the player's cfg keys of the instance's style
            // as bound (a mount's pins on them included). Kept half a second: the walk asks every frame.
            internal SoundSpec Resolve(string eventId, string variant = null)
            {
                SoundEvents.Def d = SoundEvents.Find(eventId);
                if (d == null) return null;
                string key = variant != null ? eventId + "." + variant : eventId;
                float now = Time.unscaledTime;
                if (cache.TryGetValue(key, out (SoundSpec Spec, float At) c) && now - c.At < 0.5f) return c.Spec;
                // bound here too (not only in Emit and Tick): the base reads Tuned values, which serve this instance's
                // pins only while it is bound, and the cache must never hold an unbound reading (the dev tab and the
                // self-tests call Resolve directly; a standing snail read the global keys, 2026-10-24)
                SoundSpec spec;
                using (Mount.Bind(owner))
                {
                    spec = ResolveNow(d, eventId, variant);
                }
                cache[key] = (spec, now);
                return spec;
            }

            // The layers, bottom up (docs/mount-sounds-design.md section 2): the base (the player's keys of the style,
            // a mount's pins on them included), the mount's [Mount.Sounds] then its style block, the skin's [Sounds]
            // then its style block, a mod's override. A layer's source replaces the one below; its volume and pitch
            // multiply the base's (the topmost said wins among the layers); its other fields and numbers replace.
            private SoundSpec ResolveNow(SoundEvents.Def d, string eventId, string variant)
            {
                SoundSpec r = d.Base?.Invoke(owner.S);
                bool touched = r != null;
                if (r == null) r = new SoundSpec { Layer = "none", On = true };
                else r.Layer = "base";
                float vol = 1f, pitch = 1f;
                Looks.SkinDef skin = owner.LooksRig?.DefNow;
                Catalog.Entry mount = owner.LooksRig?.MountNow ?? Catalog.Donkey;
                bool cart = owner.CurrentStyle == MountStyle.Cart;
                Looks.SkinDef mountFiles = Looks.MountSoundFileOwner(mount);
                Looks.SoundLayer style = cart ? Looks.SoundLayer.Cart : Looks.SoundLayer.Ride;
                bool baby = IsBaby;
                touched |= Apply(Looks.MountSoundBlock(mount, Looks.SoundLayer.Both)?.Get(eventId, variant), "mount", mountFiles, mount.Id, r, ref vol, ref pitch);
                touched |= Apply(Looks.MountSoundBlock(mount, style)?.Get(eventId, variant), "mount", mountFiles, mount.Id, r, ref vol, ref pitch);
                if (baby) touched |= Apply(Looks.MountSoundBlock(mount, Looks.SoundLayer.Baby)?.Get(eventId, variant), "mount", mountFiles, mount.Id, r, ref vol, ref pitch);
                touched |= Apply(Looks.SkinSoundBlock(skin, Looks.SoundLayer.Both)?.Get(eventId, variant), "skin", skin, skin?.Id, r, ref vol, ref pitch);
                touched |= Apply(Looks.SkinSoundBlock(skin, style)?.Get(eventId, variant), "skin", skin, skin?.Id, r, ref vol, ref pitch);
                if (baby) touched |= Apply(Looks.SkinSoundBlock(skin, Looks.SoundLayer.Baby)?.Get(eventId, variant), "skin", skin, skin?.Id, r, ref vol, ref pitch);
                SoundLine ov = null;
                if (variant != null) overrides.TryGetValue(eventId + "." + variant, out ov);
                if (ov == null) overrides.TryGetValue(eventId, out ov);
                touched |= Apply(ov, "mod", null, ov?.Owner ?? "mod", r, ref vol, ref pitch);
                r.Volume *= vol;
                r.Pitch *= pitch;
                return touched ? r : null;
            }

            // One layer's line onto the spec being built. fileOwner: the skin whose sounds\ folder a file line reads
            // from (a pack); a line with its own clips (the API) registers them under whoId.
            private static bool Apply(SoundLine l, string layer, Looks.SkinDef fileOwner, string whoId, SoundSpec r, ref float vol, ref float pitch)
            {
                if (l == null) return false;
                if (l.Files != null || l.Clips != null)
                {
                    string id = l.BankId;
                    if (id == null)
                    {
                        if (l.Clips != null) id = SoundBank.ForClips(layer, whoId ?? "code", l.BankKey ?? l.Key, l.Clips, l.Speech ?? false, l.Loop ?? false);
                        else if (fileOwner != null) id = SoundBank.ForSkin(fileOwner, l.BankKey ?? l.Key, l.Files, l.Speech ?? false, l.Loop ?? false);
                        l.BankId = id;
                    }
                    SoundBank.State st = SoundBank.StateOf(id);
                    if (id != null && st != SoundBank.State.Failed)
                    {
                        r.BankId = id; r.GameId = null; r.Silent = false; r.BySurface = false; r.Layer = layer;
                        r.Loading = st != SoundBank.State.Ready;
                    }
                }
                else if (l.Source == "none") { r.Silent = true; r.GameId = null; r.BankId = null; r.BySurface = false; r.Loading = false; r.Layer = layer; }
                else if (l.Source == "ground") { r.BySurface = true; r.GameId = null; r.BankId = null; r.Silent = false; r.Loading = false; r.Layer = layer; }
                else if (l.Source != null) { r.GameId = l.Source; r.BankId = null; r.Silent = false; r.BySurface = false; r.Loading = false; r.Layer = layer; }
                if (l.Volume.HasValue) vol = l.Volume.Value;
                if (l.Pitch.HasValue) pitch = l.Pitch.Value;
                if (l.PitchVariation.HasValue) r.PitchVariation = l.PitchVariation.Value;
                if (l.Start.HasValue) r.Start = l.Start.Value;
                if (l.Loop.HasValue) r.Loop = l.Loop.Value;
                if (l.Speech.HasValue) r.Speech = l.Speech.Value;
                if (l.Numbers != null) foreach (var kv in l.Numbers) r.Set(kv.Key, kv.Value);
                return true;
            }

            internal void Invalidate() => cache.Clear();

            // A mod's line for an event (or event.variant) on this instance, session-only; null takes it away.
            internal void SetOverride(string key, SoundLine line)
            {
                if (line == null) overrides.Remove(key); else overrides[key] = line;
                cache.Clear();
            }

            // The gates: the player's switches, the feature rules, the dev sheet, the standing switch.
            private bool May(SoundEvents.Def d, SoundSpec s)
            {
                if (d == null || s == null || !s.On) return false;
                if (!owner.S.SoundsOn.Value || !MountRules.Allowed(MountFeature.Sounds)) return false;
                if (d.Id == "bell" && !MountRules.Allowed(MountFeature.Bell)) return false;
                if (CargoSheet.Running) return false;
                if (!owner.IsRidden && d.StandingGated && !StandingOn) return false;
                return true;
            }

            // Whether this rig takes an event the game would otherwise play itself (the lips animation event): true
            // when it plays it (at the player's volume, silent at 0, or a layer's own sound); false hands the event
            // back to the game (SoundsOn off, a standing one with StandingSounds off, no view).
            internal bool Takes(string eventId)
            {
                try
                {
                    using (Mount.Bind(owner))
                    {
                        SoundEvents.Def d = SoundEvents.Find(eventId);
                        SoundSpec s = Resolve(eventId);
                        Wgo donkey = owner.Donkey;
                        return donkey != null && donkey && s != null && s.HasSource && May(d, s);
                    }
                }
                catch
                {
                    return false;
                }
            }

            // ------------------------------------------------------------------ playing

            // The event played now at this instance: the resolved spec, scaled by the player's master volume, the
            // instance's size (a foal: higher and softer) and the caller's scales; a stoppable event keeps its handler.
            internal SoundHandler Emit(string eventId, string variant = null, float volumeScale = 1f, float pitchScale = 1f)
            {
                try
                {
                    using (Mount.Bind(owner))
                    {
                        return EmitInner(eventId, variant, volumeScale, pitchScale);
                    }
                }
                catch (Exception e)
                {
                    Warn(eventId, $"the {eventId} sound failed: {e.Message}");
                    return null;
                }
            }

            private SoundHandler EmitInner(string eventId, string variant, float volumeScale, float pitchScale)
            {
                SoundEvents.Def d = SoundEvents.Find(eventId);
                if (d == null)
                {
                    Warn(eventId, $"no sound event '{eventId}'");
                    return null;
                }
                SoundSpec s = Resolve(eventId, variant);
                if (!May(d, s)) return null;
                Wgo donkey = owner.Donkey;
                Transform at = donkey != null && donkey ? donkey.transform : null;
                if (at == null) return null;
                if (d.Stoppable) Stop(eventId);
                if (s.Silent)
                {
                    LastEvent = Sounds.LastEvent = eventId + " (silent)";
                    return null;
                }
                if (s.Loading)
                {
                    LastEvent = Sounds.LastEvent = eventId + " (its files still loading)";
                    return null;
                }
                string id = s.BankId ?? s.GameId;
                if (id == null && s.BySurface)
                {
                    SurfaceType surface = SurfaceType.Dirt;
                    if (variant != null && Enum.TryParse(variant, true, out SurfaceType asked)) surface = asked;
                    else if (s.Number("surface_aware", 1f) > 0.5f)
                    {
                        try { surface = SurfaceUnder(at.position); } catch { surface = SurfaceType.Dirt; }
                        if (surface == SurfaceType.None) surface = SurfaceType.Dirt; // no tagged ground under the mount: still a step
                    }
                    id = StepIdFor(surface);
                }
                if (id == null) return null;
                float v = s.Volume * owner.S.SoundVolume.Value * volumeScale * VolumeScale;
                float var = s.PitchVariation;
                float p = s.Pitch * pitchScale * PitchScale * (var > 0f ? 1f + UnityEngine.Random.Range(-var, var) : 1f);
                SoundHandler h = PlayId(id, at, v, p, s.Start);
                if (h != null)
                {
                    if (d.Id == "voice" && s.BankId == null && !s.Speech)
                    {
                        // The game plays its dialogue voice in the speech group; the player's BrayInSfxChannel moves
                        // the donkey's to an ordinary sfx sound's group (as it always did: donkey_bell's).
                        Sound sfx = LazySingletonSO<AudioConfig>.Instance?.Get("donkey_bell");
                        if (sfx != null && sfx.group != null) h.SetMixerGroup(sfx.group);
                    }
                    if (d.Stoppable) loops[eventId] = h;
                    TestPlays.TryGetValue(eventId, out int n);
                    TestPlays[eventId] = n + 1;
                    if (d.Id == "voice" || d.Id == "answer")
                    {
                        nextVoiceAt = Time.time + VoiceGapS; // one voice at a time in the scene (the standing ones wait; the ridden one and the answers go anyway)
                        TestVoiceTimes.Add((Time.time, owner, d.Id));
                        if (d.Id == "voice" && !IsBaby) ScheduleAnswers();
                    }
                    if (owner.Handle != null) Mounts.Sounds.RaisePlayed(owner.Handle, eventId);
                }
                LastEvent = Sounds.LastEvent = eventId + (variant != null ? "." + variant : "") + " " + id;
                return h;
            }

            // Drawn smaller than the grown one: a baby (the [Sounds.Baby] blocks apply, the answers are its).
            internal bool IsBaby => owner.ViewScale > 0.01f && owner.ViewScale < 0.99f;

            // A smaller creature (a foal, ViewScale 0.6) is higher and softer; a grown one is as it is.
            internal float PitchScale
            {
                get
                {
                    float sc = owner.ViewScale;
                    return sc > 0.01f && Math.Abs(sc - 1f) > 0.01f ? Mathf.Pow(1f / sc, 0.7f) : 1f;
                }
            }

            internal float VolumeScale
            {
                get
                {
                    float sc = owner.ViewScale;
                    return sc > 0.01f && Math.Abs(sc - 1f) > 0.01f ? Mathf.Sqrt(sc) : 1f;
                }
            }

            internal void Stop(string eventId)
            {
                if (!loops.TryGetValue(eventId, out SoundHandler h)) return;
                loops.Remove(eventId);
                try
                {
                    if (h != null && h.IsActive) h.Stop();
                }
                catch
                {
                }
            }

            private void StopLoops()
            {
                if (loops.Count == 0) return;
                foreach (string k in new List<string>(loops.Keys)) Stop(k);
            }

            // Everything this rig keeps playing, stopped (a tier change to Idle: the view stays, the rig stops ticking).
            internal void StopAll() => StopLoops();

            private void Warn(string what, string text)
            {
                if (warned.Add(what)) Plugin.Log.LogWarning($"Sounds: {owner.Describe()}: {text}");
            }
        }
    }

    // The keeper rides the donkey in both styles, but the Cart riding pose is the game's walk animation, whose step
    // events would sound footsteps. While mounted the keeper's step events are skipped; nothing else is affected.
    [HarmonyPatch(typeof(AnimationComponentBase), nameof(AnimationComponentBase.OnAnimationStepCompleted))]
    internal static class AnimationComponentBase_OnAnimationStepCompleted_Patch
    {
        private static bool Prefix(AnimationComponentBase __instance)
        {
            try
            {
                if (!Mount.IsMounted)
                {
                    return true;
                }
                PlayerController pc = MainGame.PlayerController;
                return pc == null || !ReferenceEquals(__instance, pc.View?.PlayerAnimation);
            }
            catch
            {
                return true;
            }
        }
    }

    // The donkey's lips clip plays donkey_lips through an animation event; on a view of the mod's it goes through
    // that instance's sound rig instead (the player's volume, a skin's own sound), ridden or standing. Every other
    // caller and every other character runs the game's code, and so does an instance whose rig hands the event back
    // (sounds off, a standing one with StandingSounds off).
    [HarmonyPatch(typeof(AnimationComponentBase), nameof(AnimationComponentBase.PlaySound))]
    internal static class AnimationComponentBase_PlaySound_Patch
    {
        private static bool Prefix(AnimationComponentBase __instance, string sound)
        {
            try
            {
                Mount.Instance i = __instance != null ? Mount.ByAnimation(__instance) : null;
                if (i == null || i.SoundsRig == null || !i.SoundsRig.Takes("lips"))
                {
                    return true;
                }
                i.SoundsRig.Emit("lips");
                return false;
            }
            catch
            {
                return true;
            }
        }
    }
}
