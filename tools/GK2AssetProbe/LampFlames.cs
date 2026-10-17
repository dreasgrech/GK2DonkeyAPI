using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GK2AssetProbe
{
    // 12-13: the game's own lamp fire, copied whole: the flame (flame_fx) and sparks (sparks_fx) particle systems of its
    // lamps and lanterns (light_fx/flame, light_fx/sparks; the second run showed them as the best match), with every
    // setting as the game made them (size, life, rate, shape), so what shows is exactly the lamps' fire.
    //   12: a ring of them around the donkey, following it;
    //   13: a short burst at every step, left behind.
    // The copies are made under an inactive holder, so no component of theirs wakes up before the probe has stripped
    // everything but the particle systems (a lamp may carry the game's own scripts); then they are cloned from there.
    // "Lamp fire size" scales the copies (the lamps' own size is 1x).
    // The copies point at the game's own flame and sparks materials. Those belong to the lamp's asset bundle, which the
    // game unloads when it no longer needs a lamp: the copies then drew solid magenta squares (2026-10-17). So the
    // probe loads both materials itself through Addressables and holds them while the fire is on, which keeps their
    // bundle in memory, and gives the copies those held materials; a copy whose material is gone anyway is rebuilt.
    internal static class LampFlames
    {
        private static readonly Dictionary<string, string> MaterialKeys = new Dictionary<string, string>
        {
            { "flame_fx", "Assets/AddressableAssets/Materials/flame_fx.mat" },
            { "sparks_fx", "Assets/AddressableAssets/Materials/sparks_fx.mat" },
        };
        private static readonly List<UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<Material>> held =
            new List<UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<Material>>();
        private static float checkAt;

        // Our own hold on a game material (by its name), or null if it can't be loaded.
        private static Material Hold(string name)
        {
            string key = MaterialKeys.FirstOrDefault(k => name.StartsWith(k.Key, StringComparison.Ordinal)).Value;
            if (key == null) return null;
            var h = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<Material>(key);
            Material m = h.WaitForCompletion();
            if (m == null)
            {
                if (h.IsValid()) UnityEngine.AddressableAssets.Addressables.Release(h);
                return null;
            }
            held.Add(h);
            return m;
        }

        private static GameObject holder, flameT, sparksT;
        private static readonly List<GameObject> ring = new List<GameObject>();
        private static readonly List<Vector3> ringAt = new List<Vector3>();
        private sealed class Burst
        {
            internal GameObject Go;
            internal float StopAt, GoneAt;
        }
        private static readonly List<Burst> bursts = new List<Burst>();
        private static bool stepsOn;
        private static Vector3 lastStep;
        private static readonly float[] Sizes = { 0.5f, 0.75f, 1f, 1.5f };
        private static int size = 2; // 1x, the lamps' own
        internal static string[] SizeLabels => Array.ConvertAll(Sizes, v => v + "x");
        internal static int SizeIndex => size;
        internal static bool Trail => trail;
        // The lamp flame simulates in a Custom space (relative to a part of its lamp). Our copies don't move that part,
        // so their flames stay where they were born: a trail behind a moving donkey (the first lamp run, 2026-10-17).
        // Local keeps them on the ring; World is the trail on purpose.
        private static bool trail;
        internal static string RingLook => trail ? "trail behind" : "stay with the donkey";

        internal static bool RingOn => ring.Count > 0;
        internal static bool StepsOn => stepsOn;
        internal static string SizeName => Sizes[size] + "x";

        private static bool Templates()
        {
            if (flameT != null) return true;
            ParticleSystem f = Find("flame_fx"), s = Find("sparks_fx");
            if (f == null)
            {
                Probe.Info("no game lamp flame in memory: stand near a lit lamp (or with the cart's lantern lit) and try again");
                Probe.Status = "12/13: no game lamp flame found; see the log.";
                return false;
            }
            holder = new GameObject("assetprobe lamp templates");
            holder.SetActive(false);
            flameT = Copy(f, holder.transform);
            if (s != null) sparksT = Copy(s, holder.transform);
            return flameT != null;
        }

        // A lamp's particle system with that material; a prefab's (not in any scene) if there is one, else a live one.
        private static ParticleSystem Find(string material)
        {
            ParticleSystem best = null;
            foreach (var r in Resources.FindObjectsOfTypeAll<ParticleSystemRenderer>())
            {
                Material m = r.sharedMaterial;
                if (m == null || !m.name.StartsWith(material, StringComparison.Ordinal) || r.name.StartsWith("assetprobe", StringComparison.Ordinal)) continue;
                var ps = r.GetComponent<ParticleSystem>();
                if (ps == null) continue;
                if (best == null || (!r.gameObject.scene.IsValid() && best.gameObject.scene.IsValid())) best = ps;
            }
            return best;
        }

        private static GameObject Copy(ParticleSystem src, Transform inactive)
        {
            GameObject go = Object.Instantiate(src.gameObject, inactive, false);
            go.name = "assetprobe lamp " + src.name;
            var removed = new List<string>();
            // scripts first, then anything else that isn't the particles (a script may require a component)
            foreach (var pass in new[] { true, false })
            {
                foreach (Component c in go.GetComponentsInChildren<Component>(true))
                {
                    if (c == null || c is Transform || c is ParticleSystem || c is ParticleSystemRenderer) continue;
                    if (pass != (c is MonoBehaviour)) continue;
                    removed.Add(c.GetType().Name);
                    Object.DestroyImmediate(c);
                }
            }
            var pr = go.GetComponent<ParticleSystemRenderer>();
            Material mine = pr != null && pr.sharedMaterial != null ? Hold(pr.sharedMaterial.name) : null;
            if (mine != null) pr.sharedMaterial = mine;
            Probe.Info($"lamp {src.name}: material {(mine != null ? mine.name + " held by the probe" : "NOT held (no key for " + pr?.sharedMaterial?.name + ")")}");
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = src.transform.rotation;
            go.transform.localScale = src.transform.lossyScale;
            var main = src.main;
            var em = src.emission;
            var sh = src.shape;
            var r = src.GetComponent<ParticleSystemRenderer>();
            Probe.Info($"lamp {src.name}: copied from {Probe.PathOf(src.transform)} ({(src.gameObject.scene.IsValid() ? "scene " + src.gameObject.scene.name : "a prefab")}), " +
                $"world scale {src.transform.lossyScale}, rotation {src.transform.rotation.eulerAngles}, size {main.startSize.constant:0.##}/{main.startSize.constantMax:0.##}, " +
                $"life {main.startLifetime.constant:0.##}/{main.startLifetime.constantMax:0.##}, speed {main.startSpeed.constant:0.##}/{main.startSpeed.constantMax:0.##}, " +
                $"rate {em.rateOverTime.constant:0.#}/{em.rateOverTime.constantMax:0.#}, shape {(sh.enabled ? sh.shapeType + " r " + sh.radius.ToString("0.##") : "off")}, " +
                $"space {main.simulationSpace}{(main.simulationSpace == ParticleSystemSimulationSpace.Custom ? " (relative to " + (main.customSimulationSpace != null ? Probe.PathOf(main.customSimulationSpace) : "nothing") + ")" : "")}, scaling {main.scalingMode}, sorting {r?.sortingLayerName}/{r?.sortingOrder}, layer {src.gameObject.layer}, " +
                $"child systems {go.GetComponentsInChildren<ParticleSystem>(true).Length - 1}; removed [{string.Join(", ", removed)}]");
            return go;
        }

        private static GameObject Spawn(GameObject template, Vector3 at)
        {
            GameObject go = Object.Instantiate(template); // a root copy of an active-self object: wakes up now, already stripped
            go.name = template.name;
            go.transform.position = at;
            go.transform.localScale = template.transform.localScale * Sizes[size];
            if (size != 0)
            {
                // particle size follows the transform only with hierarchy/local scaling; set it so the size button works
                foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var m = ps.main;
                    if (m.scalingMode == ParticleSystemScalingMode.Shape) m.scalingMode = ParticleSystemScalingMode.Hierarchy;
                }
            }
            return go;
        }

        // ---------------------------------------------------------------- 12. ring

        internal static void ToggleRing()
        {
            if (ring.Count > 0)
            {
                ClearRing();
                Probe.Info("lamp fire ring off");
                return;
            }
            if (!Probe.FindDonkey() || !Templates()) return;
            BuildRing();
            Vector3 off = Probe.Center - Probe.Root.position;
            Probe.Info($"lamp fire ring on: {ring.Count} copies at {SizeName}, centred on the drawn body ({off.x:0.00} from the root's x; facing {(Probe.Body.transform.lossyScale.x < 0 ? "mirrored" : "as drawn")})");
            Probe.Status = "12: the game's lamp fire around the donkey.";
        }

        private static void BuildRing()
        {
            ClearRing();
            const int n = 6;
            const float radius = 0.8f;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                Vector3 off = new Vector3(Mathf.Cos(a) * radius, 0.05f, Mathf.Sin(a) * radius * 0.7f);
                ring.Add(SetSpace(Spawn(flameT, Probe.Center + off)));
                ringAt.Add(off);
                if (sparksT != null && i % 2 == 0)
                {
                    ring.Add(SetSpace(Spawn(sparksT, Probe.Center + off)));
                    ringAt.Add(off);
                }
            }
        }

        private static GameObject SetSpace(GameObject go)
        {
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                var m = ps.main;
                m.simulationSpace = trail ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            }
            return go;
        }

        private static void ClearRing()
        {
            foreach (var go in ring) if (go != null) Object.Destroy(go);
            ring.Clear();
            ringAt.Clear();
        }

        // ---------------------------------------------------------------- 13. steps

        internal static void ToggleSteps()
        {
            if (stepsOn)
            {
                stepsOn = false;
                Probe.Info("lamp fire steps off");
                return;
            }
            if (!Probe.FindDonkey() || !Templates()) return;
            stepsOn = true;
            lastStep = Probe.Center;
            Probe.Info($"lamp fire steps on at {SizeName}");
            Probe.Status = "13: the game's lamp fire at every step.";
        }

        internal static void SetTrail(bool on)
        {
            if (on != trail) ToggleTrail();
        }

        internal static void SetSize(int i)
        {
            if (i == size || i < 0 || i >= Sizes.Length) return;
            size = i;
            if (ring.Count > 0 && Probe.Root != null) BuildRing();
            Probe.Info("lamp fire size now " + SizeName);
        }

        internal static void ToggleTrail()
        {
            trail = !trail;
            if (ring.Count > 0 && Probe.Root != null) BuildRing();
            Probe.Info("lamp fire ring now: " + RingLook);
        }

        internal static void NextSize()
        {
            size = (size + 1) % Sizes.Length;
            if (ring.Count > 0 && Probe.Root != null) BuildRing();
            Probe.Info("lamp fire size now " + SizeName);
        }

        // ---------------------------------------------------------------- every frame

        internal static void Tick()
        {
            float now = Time.time;
            for (int i = bursts.Count - 1; i >= 0; i--)
            {
                Burst b = bursts[i];
                if (b.Go == null || now >= b.GoneAt)
                {
                    if (b.Go != null) Object.Destroy(b.Go);
                    bursts.RemoveAt(i);
                    continue;
                }
                if (b.StopAt > 0f && now >= b.StopAt)
                {
                    b.StopAt = 0f;
                    foreach (var ps in b.Go.GetComponentsInChildren<ParticleSystem>()) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                }
            }
            if (!stepsOn || Probe.Root == null || flameT == null) return;
            Vector3 p = Probe.Center;
            if ((p - lastStep).sqrMagnitude < 0.5f * 0.5f) return;
            lastStep = p;
            GameObject go = Spawn(flameT, p + Vector3.up * 0.05f);
            float life = go.GetComponentsInChildren<ParticleSystem>().Max(ps => ps.main.startLifetime.constantMax);
            bursts.Add(new Burst { Go = go, StopAt = now + 0.4f, GoneAt = now + 0.4f + life + 0.2f });
            if (bursts.Count > 30)
            {
                Object.Destroy(bursts[0].Go);
                bursts.RemoveAt(0);
            }
        }

        private static bool Broken(GameObject go)
        {
            if (go == null) return false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            return r != null && r.sharedMaterial == null;
        }

        internal static void Follow()
        {
            if ((ring.Count > 0 || stepsOn) && Time.unscaledTime >= checkAt)
            {
                checkAt = Time.unscaledTime + 1f;
                if (ring.Any(Broken) || Broken(flameT))
                {
                    bool hadRing = ring.Count > 0, hadSteps = stepsOn;
                    Probe.Warn("lamp fire: a copy lost its material (its bundle was unloaded); copying the lamp again");
                    Clear();
                    if (Probe.FindDonkey() && Templates())
                    {
                        if (hadRing) BuildRing();
                        stepsOn = hadSteps;
                    }
                }
            }
            if (ring.Count == 0) return;
            Transform root = Probe.Root;
            if (root == null)
            {
                ClearRing();
                return;
            }
            Vector3 c = Probe.Center;
            for (int i = 0; i < ring.Count; i++) if (ring[i] != null) ring[i].transform.position = c + ringAt[i];
        }

        internal static void Clear()
        {
            ClearRing();
            stepsOn = false;
            foreach (var b in bursts) if (b.Go != null) Object.Destroy(b.Go);
            bursts.Clear();
            if (holder != null) Object.Destroy(holder);
            holder = flameT = sparksT = null;
            foreach (var h in held) if (h.IsValid()) UnityEngine.AddressableAssets.Addressables.Release(h);
            held.Clear();
        }
    }
}
