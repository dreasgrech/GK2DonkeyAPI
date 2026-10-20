using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using GK2DonkeyAPI.Api;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace GK2DonkeyAPI
{
    // Particle effects on a donkey (docs/mount-looks-design.md, docs/skins-design.md §12): a system any mod registers
    // effects into, and the first effect, Hellfire, the donkey wreathed in the game's own lamp fire.
    //
    // An effect is a definition (MountEffectDef): one or more sources of particles, each with emitters placed round the
    // donkey. A source is a template GameObject of particle systems: the game's own (a lamp's flame and sparks, found by
    // their material, copied once and stripped to the particle components; their materials held through Addressables so
    // the lamp's bundle can't be unloaded under them, which made magenta squares once), or a mod's own prefab. An
    // emitter is placed on a ring (local-space particles, so they move with the donkey), at the body, or left behind as
    // the donkey moves (world space, a burst that stops and burns out). Everything spawned is an object of ours that
    // follows the donkey's body point; nothing is attached to the pooled parts, so nothing needs undoing on them, and
    // it all goes with the view (Detach) or the instance (Release).
    //
    // Per instance (look keys, kept with the save): Effect (the id, "" none) and EffectWhen (off, at night, always).
    // Everyone's: EffectTrail, EffectWithCart, EffectInFights, EffectOnFoals, the night threshold. The night test is the
    // game's sun value, the shader global "_SunLight" (what the lantern reads); no game member is used here.
    internal static partial class Effects
    {
        internal const string Section = "Effects";
        internal const string HolderName = "gk2_effect";

        internal static Tuned<string> Effect;             // per instance (a look key): the effect's id
        internal static Tuned<MountEffectWhen> When;      // per instance (a look key)
        internal static Tuned<bool> Trail, WithCart, InFights, OnFoals;
        internal static Tuned<float> NightBelow;
        internal static MountTuning Tuning;

        private static readonly int IdSunLight = Shader.PropertyToID("_SunLight");

        internal static void Bind(ConfigFile cfg)
        {
            Effect = cfg.Bind(Section, "Effect", Defaults.Get(Section, "Effect", ""),
                "The particle effect on the donkey, by id (Mounts.Effects lists them; hellfire is built in; empty: none). Set per donkey by the mods that own them; this is the value for donkeys without one of their own.");
            When = cfg.Bind(Section, "EffectWhen", Defaults.Get(Section, "EffectWhen", MountEffectWhen.Always), "When the effect shows: off, at night, or always. Never indoors.");
            Tuned.MarkLook(Effect);
            Tuned.MarkLook(When);
            Trail = cfg.Bind(Section, "EffectTrail", Defaults.Get(Section, "EffectTrail", true), "An effect with a trail leaves it behind while the donkey walks.");
            WithCart = cfg.Bind(Section, "EffectWithCart", Defaults.Get(Section, "EffectWithCart", false), "With the cart hitched, a ring goes round the donkey and the cart together (off: round the donkey only).");
            InFights = cfg.Bind(Section, "EffectInFights", Defaults.Get(Section, "EffectInFights", true), "Shown while the rider fights in armour (off: hidden in fights).");
            OnFoals = cfg.Bind(Section, "EffectOnFoals", Defaults.Get(Section, "EffectOnFoals", false), "Foals show it too (smaller), when their parent's mod puts it on them.");
            NightBelow = cfg.Bind(Section, "EffectNightBelow", Defaults.Get(Section, "EffectNightBelow", 0.35f), new ConfigDescription("\"At night\" means the game's sun value is below this (0 deep night, 1 full day).", new AcceptableValueRange<float>(0f, 1f)));
            Tuning = new MountTuning(Section, new ITuned[] { Effect, When, Trail, WithCart, InFights, OnFoals, NightBelow });
        }

        internal static void Unbind()
        {
            foreach (EffectDef d in defs.Values) d.DropTemplates();
        }

        // ------------------------------------------------------------------ the registry

        internal sealed class EffectDef
        {
            internal string Id, Name, Author, Version, Owner;
            internal bool BuiltIn;
            internal bool Foals = true;
            internal Func<MountHandle, bool> Show;   // the mod's own condition, or null
            internal readonly List<Part> Parts = new List<Part>();

            // A source's template, made on first use and dropped at Unbind (or when a material goes). A source that
            // can't make one yet (the game's particles not in memory) is asked again every few seconds.
            internal sealed class Part
            {
                internal IMountEffectSource Source;
                internal readonly List<MountEffectEmitter> Emitters = new List<MountEffectEmitter>();
                internal GameObject Template;
                internal float RetryAt;
            }

            // Parts with a template now (a ring built with fewer is rebuilt when the rest arrive).
            internal int TemplateCount
            {
                get
                {
                    int n = 0;
                    foreach (Part p in Parts) if (p.Template != null) n++;
                    return n;
                }
            }

            internal bool Ready()
            {
                bool any = false;
                foreach (Part p in Parts)
                {
                    if (p.Template != null)
                    {
                        any = true;
                        continue;
                    }
                    if (Time.unscaledTime < p.RetryAt) continue;
                    p.RetryAt = Time.unscaledTime + 5f;
                    try
                    {
                        p.Template = p.Source.MakeTemplate();
                        if (p.Template != null)
                        {
                            p.Template.name = HolderName + " template " + Id;
                            Object.DontDestroyOnLoad(p.Template);
                            p.Template.SetActive(false);
                            any = true;
                        }
                    }
                    catch (Exception e)
                    {
                        Plugin.Log.LogWarning($"Effects: '{Id}': a source failed; asked again in a while: {e.Message}");
                    }
                }
                return any;
            }

            internal void DropTemplates()
            {
                foreach (Part p in Parts)
                {
                    if (p.Template != null) Object.Destroy(p.Template);
                    p.Template = null;
                    p.RetryAt = 0f;
                    try
                    {
                        p.Source.Release();
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static readonly Dictionary<string, EffectDef> defs = new Dictionary<string, EffectDef>(StringComparer.OrdinalIgnoreCase);
        internal static IEnumerable<EffectDef> All => defs.Values;

        internal static EffectDef Find(string id)
        {
            return !string.IsNullOrEmpty(id) && defs.TryGetValue(id.Trim(), out EffectDef d) ? d : null;
        }

        internal static void Register(EffectDef d)
        {
            if (d == null || string.IsNullOrEmpty(d.Id)) throw new ArgumentException("An effect needs an id.");
            foreach (char c in d.Id)
            {
                if (!(char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_')) throw new ArgumentException($"Effect id '{d.Id}': letters, digits, dots, dashes and underscores only.");
            }
            if (d.Parts.Count == 0) throw new ArgumentException($"Effect '{d.Id}' has no part (a source with emitters).");
            if (defs.TryGetValue(d.Id, out EffectDef old) && old.Owner != d.Owner)
            {
                throw new ArgumentException($"Effect id '{d.Id}' is already registered by {old.Owner}.");
            }
            old?.DropTemplates();
            d.BuiltIn = d.Owner == Plugin.Guid;
            defs[d.Id] = d;
            Plugin.Log.LogInfo($"Effects: '{d.Id}' ({d.Name}) registered by {d.Owner}{(d.BuiltIn ? ", built in" : "")}: {d.Parts.Count} part(s).");
            Mount.ForEachInstance(i => i.EffectsRig.MarkDirty(), bind: false);
            Mounts.Effects.RaiseChanged();
        }

        internal static bool Unregister(string id, string owner)
        {
            EffectDef d = Find(id);
            if (d == null || d.Owner != owner) return false;
            defs.Remove(d.Id);
            Mount.ForEachInstance(i => i.EffectsRig.MarkDirty(), bind: false);
            d.DropTemplates();
            Plugin.Log.LogInfo($"Effects: '{d.Id}' withdrawn by {owner}.");
            Mounts.Effects.RaiseChanged();
            return true;
        }

        // Hellfire: the lamps' flame on a ring of six and their sparks on three of those spots, a flame left behind.
        // Defined through the public API exactly as a mod would (docs/api.md "Effects"): nothing here a mod can't do.
        // Called from Plugin after the dev keys bind, only with the mounts switched on (the API keeps it out otherwise).
        internal static void RegisterBuiltIn()
        {
            MountEffects api = Mounts.Effects;
            var d = new MountEffectDef { Id = "hellfire", Name = "Hellfire", Author = "dreas", Version = "1.0" };
            var flame = new MountEffectPart { Source = api.GameParticles("flame_fx", "Assets/AddressableAssets/Materials/flame_fx.mat") };
            flame.Emitters.Add(new MountEffectEmitter { Placement = MountEffectPlacement.Ring, Count = 6, Radius = 0.8f, Size = 1.5f, Height = 0.05f });
            flame.Emitters.Add(new MountEffectEmitter { Placement = MountEffectPlacement.Trail, Size = 1.2f, Height = 0.05f, Every = 0.5f, Burst = 0.4f });
            var sparks = new MountEffectPart { Source = api.GameParticles("sparks_fx", "Assets/AddressableAssets/Materials/sparks_fx.mat") };
            sparks.Emitters.Add(new MountEffectEmitter { Placement = MountEffectPlacement.Ring, Count = 3, Radius = 0.8f, Size = 1.5f, Height = 0.05f });
            d.Parts.Add(flame);
            d.Parts.Add(sparks);
            api.Register(Plugin.Guid, d);
        }

        // The effects to pick from in a settings list: the mount's own, none, then every registered effect.
        internal static IReadOnlyList<(string Id, string Label)> EffectChoices()
        {
            var list = new List<(string, string)> { (MountEffects.KindId, "As the mount or skin"), (MountEffects.NoneId, "None") };
            foreach (MountEffectInfo e in Mounts.Effects.Effects) list.Add((e.Id, e.Name));
            return list;
        }

        // For the self-tests: every emitter of ours in the world (the rings' and the trails', parented or not).
        internal static int TestCountInWorld()
        {
            int n = 0;
            foreach (ParticleSystem ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (ps != null && ps.transform.root.name.StartsWith(HolderName, StringComparison.Ordinal) && !ps.transform.root.name.Contains("template")) n++;
            }
            return n;
        }

        // ------------------------------------------------------------------ the game's own particles as a source

        // A particle system of the game's, found by the material it is drawn with (a prefab's rather than a live one),
        // copied and stripped to its particle components; the material held through Addressables while in use.
        internal sealed class GameParticles : IMountEffectSource
        {
            private readonly string materialName, materialKey;
            private readonly List<AsyncOperationHandle<Material>> held = new List<AsyncOperationHandle<Material>>();
            private bool warnedMissing;

            internal GameParticles(string materialName, string addressableMaterialKey)
            {
                this.materialName = materialName;
                materialKey = addressableMaterialKey;
            }

            public GameObject MakeTemplate()
            {
                ParticleSystem src = null;
                foreach (ParticleSystemRenderer r in Resources.FindObjectsOfTypeAll<ParticleSystemRenderer>())
                {
                    Material m = r.sharedMaterial;
                    if (m == null || !m.name.StartsWith(materialName, StringComparison.Ordinal)) continue;
                    if (r.transform.root.name.StartsWith(HolderName, StringComparison.Ordinal)) continue;
                    var ps = r.GetComponent<ParticleSystem>();
                    if (ps == null) continue;
                    if (src == null || (!r.gameObject.scene.IsValid() && src.gameObject.scene.IsValid())) src = ps;
                }
                if (src == null)
                {
                    if (!warnedMissing) Plugin.Log.LogWarning($"Effects: no particle system drawn with '{materialName}' is in memory yet (a lamp's, loaded with any donkey view); asked again every few seconds, said once.");
                    warnedMissing = true;
                    return null;
                }
                warnedMissing = false;
                var inactive = new GameObject(HolderName + " copying");
                inactive.SetActive(false);
                GameObject go = Object.Instantiate(src.gameObject, inactive.transform, false);
                foreach (bool scripts in new[] { true, false })
                {
                    foreach (Component c in go.GetComponentsInChildren<Component>(true))
                    {
                        if (c == null || c is Transform || c is ParticleSystem || c is ParticleSystemRenderer) continue;
                        if (scripts != (c is MonoBehaviour)) continue;
                        Object.DestroyImmediate(c);
                    }
                }
                var pr = go.GetComponent<ParticleSystemRenderer>();
                if (pr != null && pr.sharedMaterial != null)
                {
                    Material mine = Hold();
                    if (mine != null) pr.sharedMaterial = mine;
                }
                go.transform.SetParent(null, false);
                Object.Destroy(inactive);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = src.transform.rotation;
                go.transform.localScale = src.transform.lossyScale;
                foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var m = ps.main;
                    if (m.scalingMode == ParticleSystemScalingMode.Shape) m.scalingMode = ParticleSystemScalingMode.Hierarchy; // the size applies through the transform
                }
                return go;
            }

            private Material Hold()
            {
                try
                {
                    AsyncOperationHandle<Material> h = Addressables.LoadAssetAsync<Material>(materialKey);
                    Material m = h.WaitForCompletion();
                    if (m == null)
                    {
                        if (h.IsValid()) Addressables.Release(h);
                        return null;
                    }
                    held.Add(h);
                    return m;
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"Effects: the material {materialKey} could not be held: {e.Message}");
                    return null;
                }
            }

            public void Release()
            {
                foreach (var h in held) if (h.IsValid()) Addressables.Release(h);
                held.Clear();
            }
        }

        private static GameObject Spawn(GameObject template, Transform parent, Vector3 pos, float size, bool local)
        {
            GameObject go = Object.Instantiate(template, parent, false);
            go.name = HolderName + " emitter";
            if (parent != null) go.transform.localPosition = pos; else go.transform.position = pos;
            go.transform.localScale = template.transform.localScale * size;
            foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                var m = ps.main;
                m.simulationSpace = local ? ParticleSystemSimulationSpace.Local : ParticleSystemSimulationSpace.World;
            }
            go.SetActive(true);
            return go;
        }

        internal static bool Night()
        {
            try
            {
                return Shader.GetGlobalFloat(IdSunLight) < NightBelow.Value;
            }
            catch
            {
                return false;
            }
        }

        // ------------------------------------------------------------------ the rig: one per instance

        internal sealed class Rig
        {
            private readonly Mount.Instance owner;
            private EffectDef effect;
            private GameObject holder;                         // follows the body; the ring and body emitters are its children
            private readonly List<GameObject> emitters = new List<GameObject>();
            private readonly List<(MountEffectEmitter E, GameObject T)> trailEmitters = new List<(MountEffectEmitter, GameObject)>();
            // The trail's pieces, pooled: each made once, replayed in place (no Instantiate/Destroy while walking).
            private sealed class TrailPiece
            {
                internal GameObject Go;
                internal ParticleSystem[] Systems;
                internal int Emitter;
                internal float Life, StopAt, GoneAt;
                internal bool Active;
            }
            private readonly List<TrailPiece> trail = new List<TrailPiece>();
            private const int TrailMax = 40;
            private float builtScale = -1f;
            private float builtWidth = -1f;
            private int builtTemplates;
            private string lit;
            private bool builtWithCart, dirty = true, failed;
            private Vector3 lastStep;
            private float nextCheck;

            internal Rig(Mount.Instance owner)
            {
                this.owner = owner;
            }

            internal bool Lit => holder != null;
            internal string Wearing => effect?.Id ?? "";
            internal string Note => failed ? "off (failed)" : holder != null ? effect.Id + " lit" : effect != null ? effect.Id + " (out)" : "none";
            internal void MarkDirty() => dirty = true;

            // Every tick of the instance (the plugin's LateUpdate): which effect, whether it shows, and where.
            internal void Tick()
            {
                if (failed) return;
                try
                {
                    TickInner();
                }
                catch (Exception e)
                {
                    failed = true;
                    Plugin.Log.LogWarning($"Effects: {owner.Describe()}: the effect failed, off for this view: {e.Message}");
                    Detach();
                }
            }

            private void TickInner()
            {
                string want;
                MountEffectWhen when;
                bool onFoals, inFights, withCart, trailOn;
                using (Mount.Bind(owner))
                {
                    want = MountEffects.IsNone(Effect.Value) ? "" : Effect.Value.Trim();
                    when = When.Value;
                    if (want.Equals(MountEffects.KindId, StringComparison.OrdinalIgnoreCase))
                    {
                        // "As the mount": the wanted skin's own effect first (Hellfire comes with its fire), else the
                        // mount's; none if neither has one (docs/mounts-parity-matrix.md 9.5)
                        Looks.SkinDef sd = owner.LooksRig.DefNow;
                        Kinds.KindDef k = owner.LooksRig.KindNow;
                        if (sd != null && !string.IsNullOrEmpty(sd.Effect))
                        {
                            want = sd.Effect;
                            when = sd.EffectWhen;
                        }
                        else
                        {
                            want = k.Effect ?? "";
                            if (want.Length > 0) when = k.EffectWhen;
                        }
                    }
                    onFoals = OnFoals.Value;
                    inFights = InFights.Value;
                    withCart = WithCart.Value && owner.HasCart;
                    trailOn = Trail.Value;
                }
                if (dirty || !string.Equals(want, effect?.Id ?? "", StringComparison.OrdinalIgnoreCase))
                {
                    dirty = false;
                    EffectDef d = Find(want);
                    if (want.Length > 0 && d == null && missingLogged.Add(want))
                    {
                        // an Info, as a missing skin: the mod that had the effect is gone, nothing here to fix
                        if (Plugin.MountsOn) Plugin.Log.LogInfo($"Effects: no effect '{want}' is registered (its mod isn't loaded); the donkey shows none (the choice is kept and comes back with it).");
                        else Plugin.Log.LogInfo($"Effects: the effect '{want}' is set, but the effects are off in this version (Debug.MountsPreview); the donkey shows none (the choice is kept)."); // expected while off: no warning
                    }
                    if (d != effect)
                    {
                        Clear();
                        ClearTrail();
                        effect = d;
                    }
                }
                bool show = effect != null && owner.HasDonkey && owner.HasView && when != MountEffectWhen.Off
                    && (owner.ViewScale >= 1f || (onFoals && effect.Foals))
                    && (when != MountEffectWhen.Night || Night())
                    && !Mount.Instance.IsIndoors()
                    && (inFights || !owner.IsRidden || !Mount.Instance.RiderInArmour())
                    && (effect.Show == null || ShowSays());
                if (!show)
                {
                    if (holder != null) Clear();
                    TickTrail(false, false);
                    return;
                }
                if (!effect.Ready())
                {
                    return;
                }
                float scale = owner.ViewScale < 1f ? owner.ViewScale : 1f;
                float width = owner.LooksRig.EffectScale; // the ring's size against the donkey's: the worn skin's drawn width
                if (holder == null || !Mathf.Approximately(scale, builtScale) || !Mathf.Approximately(width, builtWidth) || withCart != builtWithCart || effect.TemplateCount != builtTemplates)
                {
                    Build(scale, withCart, width);
                    if (lit != effect.Id)
                    {
                        lit = effect.Id; // once per effect per instance, so the log says what burns where
                        Plugin.Log.LogInfo($"Effects: {owner.Describe()}: '{effect.Id}' lit ({emitters.Count} emitters{(trailEmitters.Count > 0 ? ", a trail" : "")}{(withCart ? ", round the cart" : "")}).");
                    }
                }
                holder.transform.position = Centre(withCart);
                if (Time.unscaledTime >= nextCheck)
                {
                    nextCheck = Time.unscaledTime + 1f;
                    foreach (GameObject g in emitters)
                    {
                        var r = g != null ? g.GetComponentInChildren<ParticleSystemRenderer>() : null;
                        if (r != null && r.sharedMaterial == null)
                        {
                            Plugin.Log.LogWarning($"Effects: '{effect.Id}': an emitter lost its material (its bundle was unloaded); the effect is rebuilt.");
                            effect.DropTemplates();
                            Clear();
                            return;
                        }
                    }
                }
                TickTrail(true, trailOn);
            }

            private static readonly HashSet<string> missingLogged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // The effect's own condition (a mod's rule, ANDed with the built-in ones); a throw counts as "show", logged once.
            private bool ShowSays()
            {
                try
                {
                    return effect.Show(owner.Handle);
                }
                catch (Exception e)
                {
                    if (missingLogged.Add("show:" + effect.Id)) Plugin.Log.LogWarning($"Effects: '{effect.Id}': its Show rule threw ({e.Message}); treated as shown.");
                    return true;
                }
            }

            private Vector3 Centre(bool withCart)
            {
                Vector3 body = owner.BodyPoint();
                return withCart ? (body + owner.CartPoint()) * 0.5f : body;
            }

            private void Build(float scale, bool withCart, float width = 1f)
            {
                Clear();
                holder = new GameObject(HolderName);
                builtScale = scale;
                builtWidth = width;
                builtWithCart = withCart;
                builtTemplates = effect.TemplateCount;
                foreach (EffectDef.Part p in effect.Parts)
                {
                    if (p.Template == null) continue;
                    foreach (MountEffectEmitter e in p.Emitters)
                    {
                        switch (e.Placement)
                        {
                            case MountEffectPlacement.Ring:
                                float radius = e.Radius * (withCart ? 1.6f : 1f) * scale * width;
                                for (int i = 0; i < Mathf.Max(1, e.Count); i++)
                                {
                                    float a = i * Mathf.PI * 2f / Mathf.Max(1, e.Count) + e.Angle * Mathf.Deg2Rad;
                                    var off = new Vector3(Mathf.Cos(a) * radius, e.Height, Mathf.Sin(a) * radius * 0.7f) + e.Offset * scale;
                                    emitters.Add(Spawn(p.Template, holder.transform, off, e.Size * scale, local: true));
                                }
                                break;
                            case MountEffectPlacement.Body:
                                emitters.Add(Spawn(p.Template, holder.transform, new Vector3(0f, e.Height, 0f) + e.Offset * scale, e.Size * scale, local: true));
                                break;
                            case MountEffectPlacement.Trail:
                                trailEmitters.Add((e, p.Template));
                                break;
                        }
                    }
                }
                lastStep = owner.BodyPoint();
            }

            private void Clear()
            {
                if (holder != null) Object.Destroy(holder);
                holder = null;
                emitters.Clear();
                trailEmitters.Clear();
                builtScale = -1f;
            }

            // The pieces age (emitting stops at StopAt, the piece is parked at GoneAt) and a new one is lit every Every
            // units walked, taken from the parked ones when there is one: nothing is made or destroyed in steady state.
            private void TickTrail(bool lit, bool trailOn)
            {
                float now = Time.time;
                for (int i = 0; i < trail.Count; i++)
                {
                    TrailPiece t = trail[i];
                    if (!t.Active) continue;
                    if (t.Go == null)
                    {
                        t.Active = false;
                        continue;
                    }
                    if (now >= t.GoneAt)
                    {
                        Park(t);
                    }
                    else if (t.StopAt > 0f && now >= t.StopAt)
                    {
                        foreach (ParticleSystem ps in t.Systems) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                        t.StopAt = 0f;
                    }
                }
                if (!lit || !trailOn || trailEmitters.Count == 0) return;
                Vector3 p = owner.BodyPoint();
                float every = trailEmitters[0].E.Every > 0f ? trailEmitters[0].E.Every : 0.5f;
                if ((p - lastStep).sqrMagnitude < every * every) return;
                lastStep = p;
                for (int k = 0; k < trailEmitters.Count; k++)
                {
                    (MountEffectEmitter e, GameObject template) = trailEmitters[k];
                    Vector3 at = p + Vector3.up * e.Height + e.Offset * builtScale;
                    TrailPiece t = null;
                    int active = 0;
                    TrailPiece oldest = null;
                    foreach (TrailPiece c in trail)
                    {
                        if (c.Emitter != k || c.Go == null) continue;
                        if (!c.Active)
                        {
                            if (t == null) t = c;
                        }
                        else
                        {
                            active++;
                            if (oldest == null || c.GoneAt < oldest.GoneAt) oldest = c;
                        }
                    }
                    if (t == null && active >= TrailMax && oldest != null)
                    {
                        Park(oldest);
                        t = oldest;
                    }
                    if (t == null)
                    {
                        GameObject go = Spawn(template, null, at, e.Size * builtScale, local: false);
                        t = new TrailPiece { Go = go, Systems = go.GetComponentsInChildren<ParticleSystem>(true), Emitter = k, Life = 1f };
                        foreach (ParticleSystem ps in t.Systems) t.Life = Mathf.Max(t.Life, ps.main.startLifetime.constantMax);
                        trail.Add(t);
                    }
                    else
                    {
                        t.Go.transform.position = at;
                        t.Go.SetActive(true);
                        foreach (ParticleSystem ps in t.Systems) ps.Play(false);
                    }
                    float burst = e.Burst > 0f ? e.Burst : 0.4f;
                    t.StopAt = now + burst;
                    t.GoneAt = now + burst + t.Life + 0.2f;
                    t.Active = true;
                }
            }

            private static void Park(TrailPiece t)
            {
                t.Active = false;
                if (t.Go == null) return;
                foreach (ParticleSystem ps in t.Systems) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                t.Go.SetActive(false);
            }

            private void ClearTrail()
            {
                foreach (TrailPiece t in trail) if (t.Go != null) Object.Destroy(t.Go);
                trail.Clear();
            }

            // The view goes (ReleaseView): nothing of ours stays in the world.
            internal void Detach()
            {
                Clear();
                ClearTrail();
            }

            internal void Release()
            {
                Detach();
                effect = null;
                failed = false;
                dirty = true;
            }

            internal int TestEmitters => emitters.Count;
            // The ring's reach along the body (the self-tests): the farthest emitter from the holder's centre along x.
            internal float TestRingRadiusX
            {
                get
                {
                    float best = 0f;
                    foreach (GameObject g in emitters) if (g != null && g) best = Mathf.Max(best, Mathf.Abs(g.transform.localPosition.x));
                    return best;
                }
            }
            // For the self-tests: where this rig's ring is (null: none), and how much of this rig is in the world (its
            // ring and its trail pieces), so another donkey's fire doesn't count.
            internal Vector3? TestRingPosition => holder != null ? holder.transform.position : (Vector3?)null;

            internal int TestInWorld
            {
                get
                {
                    int n = holder != null ? emitters.Count : 0;
                    foreach (TrailPiece t in trail) if (t.Go != null) n++;
                    return n;
                }
            }
        }
    }
}
