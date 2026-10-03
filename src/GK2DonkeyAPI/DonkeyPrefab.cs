using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // The game's donkey prefab as the measure of a clean view (docs/pooled-parts.md). Our donkeys are built from parts
    // the game pools and reuses (its delivery donkey shares them), and from views the mod parks itself (ViewPool). A part
    // that comes back changed carries the change into the next donkey; three bugs of 2026-10-03 were that: a live
    // collider that threw the keeper up in a doorway, a stale animator facing, and a head sprite the animator took as its
    // default (the "double donkey"). So:
    //   - ResetSprites, when a view is prepared: every sprite as the prefab has it, before the animator is bound again,
    //     so what the animator takes as its defaults is the prefab's;
    //   - Audit, when a view goes back (ReleaseView, after the mod's own restores): what the mod ever changes, compared
    //     with the prefab, put right and logged ([pool]), so a leftover shows in the log the moment it happens.
    // Paths are relative to the part's root (MainWgoPart, the prefab's instance), each step with its sibling index, so
    // two children of one name stay apart; children the mod adds come after the prefab's and don't shift them.
    internal static class DonkeyPrefab
    {
        private sealed class Node
        {
            internal bool Active;
            internal Sprite Sprite;              // SpriteRenderers only
            internal bool HasSprite;
            internal bool HasCollider;           // Colliders only
            internal int Layer;
        }

        private static Dictionary<string, Node> baseline;
        private static bool tried, failed;
        private static int auditsLogged, resetsLogged;

        // Loaded once, synchronously (the prefab is in memory whenever a donkey view exists).
        private static bool Ready()
        {
            if (baseline != null)
            {
                return true;
            }
            if (tried)
            {
                return false;
            }
            tried = true;
            try
            {
                var handle = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<GameObject>("Assets/AddressableAssets/WGOs/" + Plugin.MountWgoId.Value + ".prefab");
                GameObject prefab = handle.WaitForCompletion();
                if (prefab == null)
                {
                    throw new Exception("not found");
                }
                var b = new Dictionary<string, Node>(StringComparer.Ordinal);
                Walk(prefab.transform, "", b);
                baseline = b;
                Plugin.Log.LogInfo($"Mount: the donkey prefab's clean state read ({b.Count} nodes); views are reset to it and checked against it when they go back.");
                return true;
            }
            catch (Exception e)
            {
                failed = true;
                Plugin.Log.LogWarning("Mount: the donkey prefab could not be read (" + e.Message + "); views are not checked against it this session.");
                return false;
            }
        }

        private static void Walk(Transform t, string path, Dictionary<string, Node> into)
        {
            var n = new Node { Active = t.gameObject.activeSelf, Layer = t.gameObject.layer };
            SpriteRenderer sr = t.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                n.HasSprite = true;
                n.Sprite = sr.sprite;
            }
            n.HasCollider = t.GetComponent<Collider>() != null;
            into[path] = n;
            for (int i = 0; i < t.childCount; i++)
            {
                Transform ch = t.GetChild(i);
                Walk(ch, path + "/" + i + ":" + ch.name, into);
            }
        }

        private static void Each(Transform t, string path, Action<Transform, string> a)
        {
            a(t, path);
            for (int i = 0; i < t.childCount; i++)
            {
                Transform ch = t.GetChild(i);
                Each(ch, path + "/" + i + ":" + ch.name, a);
            }
        }

        // Every sprite as the prefab has it; then the caller binds the animator again. Returns the names that differed.
        internal static string ResetSprites(Transform part)
        {
            if (part == null || !Ready())
            {
                return null;
            }
            var changed = new StringBuilder();
            Each(part, "", (t, path) =>
            {
                if (!baseline.TryGetValue(path, out Node n) || !n.HasSprite)
                {
                    return;
                }
                SpriteRenderer sr = t.GetComponent<SpriteRenderer>();
                if (sr != null && sr.sprite != n.Sprite)
                {
                    changed.Append(changed.Length > 0 ? ", " : "").Append(t.name).Append(' ')
                        .Append(sr.sprite != null ? sr.sprite.name : "none").Append(" -> ").Append(n.Sprite != null ? n.Sprite.name : "none");
                    sr.sprite = n.Sprite;
                }
            });
            return changed.ToString();
        }

        internal static void LogReset(string who, string changed)
        {
            if (!string.IsNullOrEmpty(changed) && resetsLogged < 5)
            {
                resetsLogged++;
                Mount.DiagLog($"[view] {who}: sprites set back to the prefab's before the animator is bound ({changed}).");
            }
        }

        // What the mod changes on a view, compared with the prefab once the mod has put its own changes back: renderers
        // forced off, colliders (on/off, layer) and the navmesh cuts' objects, the head sprite (not animated by every
        // clip: the bound default), the animators (on, keep-state, speed), and objects the mod added. Each difference is
        // put right and named in the log.
        internal static void Audit(Transform part, string who)
        {
            if (part == null || failed || !Ready())
            {
                return;
            }
            var found = new List<string>();
            var extra = new List<Transform>();
            Each(part, "", (t, path) =>
            {
                if (!baseline.TryGetValue(path, out Node n))
                {
                    if (t != part && baseline.ContainsKey(ParentPath(path)))
                    {
                        extra.Add(t); // an object that isn't the prefab's, under one that is: added by someone
                    }
                    return;
                }
                foreach (Renderer r in t.GetComponents<Renderer>())
                {
                    if (r.forceRenderingOff)
                    {
                        found.Add(t.name + " forced off");
                        r.forceRenderingOff = false;
                    }
                }
                if (n.HasCollider)
                {
                    // Only the layer: whether a collider is on is also the game's (the interaction collider follows the
                    // data's IsInteractable), and RestoreColliders puts back what the view had when it came.
                    if (t.gameObject.layer != n.Layer)
                    {
                        found.Add($"{t.name} layer {t.gameObject.layer}");
                        t.gameObject.layer = n.Layer;
                    }
                }
                if (t.GetComponent<CustomNavMeshCutBakeSourceBase>() != null && t.gameObject.activeSelf != n.Active)
                {
                    found.Add($"{t.name} cut {(t.gameObject.activeSelf ? "on" : "off")}");
                    t.gameObject.SetActive(n.Active);
                }
                if (n.HasSprite && t.name == "hed")
                {
                    SpriteRenderer sr = t.GetComponent<SpriteRenderer>();
                    if (sr != null && sr.sprite != n.Sprite)
                    {
                        found.Add($"hed sprite {(sr.sprite != null ? sr.sprite.name : "none")}");
                        sr.sprite = n.Sprite;
                    }
                }
                Animator a = t.GetComponent<Animator>();
                if (a != null)
                {
                    if (!a.enabled)
                    {
                        found.Add(t.name + " animator off");
                        a.enabled = true;
                    }
                    if (Mathf.Abs(a.speed - 1f) > 0.001f)
                    {
                        found.Add($"{t.name} animator speed {a.speed:F2}");
                        a.speed = 1f;
                    }
                }
            });
            foreach (Transform t in extra)
            {
                if (Array.IndexOf(ModObjects, t.name) >= 0)
                {
                    found.Add("mod object " + t.name);
                    try
                    {
                        UnityEngine.Object.Destroy(t.gameObject);
                    }
                    catch
                    {
                    }
                }
                else if (unknownSeen.Add(t.name))
                {
                    // Maybe the game's own (made at run time): named once, never touched.
                    Plugin.Log.LogInfo($"[pool] {who}: its view has an object the prefab hasn't, left as it is: {t.name} under {t.parent?.name}.");
                }
            }
            if (found.Count > 0 && auditsLogged < 20)
            {
                auditsLogged++;
                Plugin.Log.LogWarning($"[pool] {who}: its view went back to the pool with {found.Count} leftover(s), put right: {string.Join("; ", found)}. A mod change was not undone; please report this line.");
            }
        }

        // What the mod adds to a view (RiderLeg, LampHang, Cargo): removed by their own Detach; one still there is a leftover.
        private static readonly string[] ModObjects = { "mount_rider_legs", "gk2_hanging_lamp", Cargo.ContainerName };
        private static readonly HashSet<string> unknownSeen = new HashSet<string>(StringComparer.Ordinal);

        private static string ParentPath(string path)
        {
            int i = path.LastIndexOf('/');
            return i <= 0 ? "" : path.Substring(0, i);
        }
    }
}
