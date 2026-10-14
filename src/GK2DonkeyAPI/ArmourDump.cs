using System;
using System.Text;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // The keeper's animator in his armour view, written to the log once on foot and once mounted (2026-10-14: the
    // mounted keeper in armour shows as a blue silhouette with the body renderer resolving to no sprite and the leg
    // layer on a "static, facing down" frame while he faces right; re-applying the pose did not change it). The two
    // dumps, parameters, every layer's state and clip, every sprite layer, are compared to find what differs when
    // mounted. Temporary: goes once the cause is known (docs/mounted-combat.md §9.7).
    internal static partial class Mount
    {
        private static int armourDumpsOnFoot;
        private static int armourDumpsMounted;
        private static int armourDumpDueFrame = -1;
        private static bool armourDumpDueMounted;

        // Each registry frame: in armour and not mounted, one dump a second after the armour went on (the layers settled).
        internal static void ArmourDumpOnFoot(PlayerController pc)
        {
            if (IsMounted || armourDumpsOnFoot >= 1 || !Instance.InArmour(pc))
            {
                armourDumpDueFrame = IsMounted || armourDumpsOnFoot >= 1 ? armourDumpDueFrame : -1;
                return;
            }
            ScheduleOrRunArmourDump(pc, mounted: false);
        }

        // From the ridden instance's tick while in armour: the same, once per session for the first two mounts.
        internal static void ArmourDumpMounted(PlayerController pc)
        {
            if (armourDumpsMounted >= 2)
            {
                return;
            }
            ScheduleOrRunArmourDump(pc, mounted: true);
        }

        private static void ScheduleOrRunArmourDump(PlayerController pc, bool mounted)
        {
            if (armourDumpDueFrame < 0 || armourDumpDueMounted != mounted)
            {
                armourDumpDueFrame = Time.frameCount + 60;
                armourDumpDueMounted = mounted;
                return;
            }
            if (Time.frameCount < armourDumpDueFrame)
            {
                return;
            }
            armourDumpDueFrame = -1;
            if (mounted)
            {
                armourDumpsMounted++;
            }
            else
            {
                armourDumpsOnFoot++;
            }
            DumpKeeperAnimator(pc, mounted ? "mounted" : "on foot");
        }

        internal static void DumpKeeperAnimator(PlayerController pc, string tag)
        {
            try
            {
                Animator a = pc?.View?.PlayerAnimation?.Animator;
                if (a == null)
                {
                    Plugin.Log.LogInfo($"[armour] {tag}: no animator.");
                    return;
                }
                var sb = new StringBuilder();
                sb.AppendLine($"[armour] keeper animator, {tag}: armour {Instance.InArmour(pc)}, mounted {IsMounted}, charState {pc.PlayerData.charState.Value}, direction {pc.PlayerData.Direction:F2}, drawn {pc.View.PlayerAnimation.GetDirection():F2}, animator speed {a.speed:F2}, enabled {a.isActiveAndEnabled}, root {a.transform.name} local {a.transform.localPosition:F3} scale {a.transform.localScale:F2}");
                sb.AppendLine($"  parameters ({a.parameterCount}):");
                foreach (AnimatorControllerParameter p in a.parameters)
                {
                    string v;
                    switch (p.type)
                    {
                        case AnimatorControllerParameterType.Float: v = a.GetFloat(p.nameHash).ToString("F3"); break;
                        case AnimatorControllerParameterType.Int: v = a.GetInteger(p.nameHash).ToString(); break;
                        case AnimatorControllerParameterType.Bool: v = a.GetBool(p.nameHash).ToString(); break;
                        default: v = "(trigger)"; break;
                    }
                    sb.AppendLine($"    {p.type} {p.name} = {v}");
                }
                sb.AppendLine($"  layers ({a.layerCount}):");
                for (int i = 0; i < a.layerCount; i++)
                {
                    AnimatorStateInfo st = a.GetCurrentAnimatorStateInfo(i);
                    AnimatorClipInfo[] ci = a.GetCurrentAnimatorClipInfo(i);
                    string clip = ci.Length > 0 && ci[0].clip != null ? ci[0].clip.name : "-";
                    AnimatorStateInfo nx = a.GetNextAnimatorStateInfo(i);
                    string next = a.IsInTransition(i) ? $" -> transition to {nx.shortNameHash}" : "";
                    sb.AppendLine($"    [{i}] {a.GetLayerName(i)} weight={a.GetLayerWeight(i):F2} state={st.shortNameHash} loop={st.loop} nTime={st.normalizedTime:F2} clip={clip}{next}");
                }
                sb.AppendLine("  sprite layers:");
                foreach (SpriteRenderer sr in a.GetComponentsInChildren<SpriteRenderer>(includeInactive: true))
                {
                    Transform t = sr.transform;
                    string path = t.name;
                    while (t.parent != null && t.parent != a.transform)
                    {
                        t = t.parent;
                        path = t.name + "/" + path;
                    }
                    sb.AppendLine($"    {path}: {(sr.gameObject.activeInHierarchy ? "active" : "inactive")} {(sr.enabled ? "on" : "off")} sprite={(sr.sprite != null ? sr.sprite.name : "none")} flipX={sr.flipX} local={sr.transform.localPosition:F3} colour={sr.color}");
                }
                Plugin.Log.LogInfo(sb.ToString().TrimEnd());
            }
            catch (Exception e)
            {
                Plugin.Log.LogInfo($"[armour] dump failed: {e.Message}");
            }
        }
    }
}
