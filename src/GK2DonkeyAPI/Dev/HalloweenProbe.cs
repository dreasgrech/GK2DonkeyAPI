using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // The game's Halloween head table on the zombie pictures (docs/game-update-26404424.md "Halloween"; the user,
    // 2026-11-04: "invesitgate this more"). HalloweenHeadSkinOverride swaps head 1051 for 1060 from 24 October to 7
    // November, and DropViewAtomMesh.ActivateZombie picks a lying or carried zombie's picture by the swapped id (the
    // ground drops' atom, BigItem.prefab, carries pose1/pose2_t1060 embedded; the catalog names 1050-1059). A probe, no checks: the zombie picture lists of the keeper's overhead atom
    // and of a ground drop's atom, and what a copy of each draws for heads 1050 and 1051 with the swap forced off and on
    // (a prefix on Resolve for this scenario only, so it runs on any date), fresh and reused after head 1050 (a view that
    // showed a zombie already), and the cart's name check on it (Cargo.cs: a cart view is a copy of the overhead atom with
    // isOverhead off, its picture's name compared with drops_item_zombie_pose2_t<head>).
    internal static class HalloweenProbe
    {
        private static readonly AccessTools.FieldRef<DropViewAtomMesh, List<DropViewAtomMeshElement>> DropList =
            AccessTools.FieldRefAccess<DropViewAtomMesh, List<DropViewAtomMeshElement>>("zombieWorkerDropPrefabs");
        private static readonly AccessTools.FieldRef<DropViewAtomMesh, List<DropViewAtomMeshElement>> OverheadList =
            AccessTools.FieldRefAccess<DropViewAtomMesh, List<DropViewAtomMeshElement>>("zombieWorkerOverheadPrefabs");
        private static readonly AccessTools.FieldRef<DropView, DropViewAtomBase> BigAtom =
            AccessTools.FieldRefAccess<DropView, DropViewAtomBase>("bigItem");

        internal static IEnumerator Heads()
        {
            DropViewAtomMesh overhead = Cargo.FindSourceAtom();
            if (overhead == null)
            {
                SelfTest.Skip("the keeper's overhead atom was not found");
                yield break;
            }
            var swapped = new List<string>();
            for (int h = 1040; h <= 1070; h++)
            {
                int to = HalloweenHeadSkinOverride.Resolve(h);
                if (to != h) swapped.Add(h + " -> " + to);
            }
            SelfTest.Info($"the event by today's date: {TemporaryEventDates.IsHalloweenEvent()}; heads 1040-1070 swapped now: {(swapped.Count == 0 ? "none" : string.Join(", ", swapped))}");
            SelfTest.Info("the keeper's overhead atom: " + Lists(overhead));
            DropViewAtomMesh ground = null;
            int grounds = 0;
            foreach (DropView dv in UnityEngine.Object.FindObjectsByType<DropView>(FindObjectsSortMode.None))
            {
                if (BigAtom(dv) is DropViewAtomMesh m)
                {
                    grounds++;
                    if (ground == null) ground = m;
                }
            }
            SelfTest.Info(ground == null ? "no ground drop with a big atom in the scene: the ground pictures not measured"
                : $"{grounds} ground drop(s) with a big atom; the first's ({ground.transform.parent?.name ?? ground.name}): " + Lists(ground));
            var holder = new GameObject("gk2donkeyapi halloween probe");
            holder.transform.position = new Vector3(0f, -10000f, 0f); // out of sight for the frame it stands
            try
            {
                if (!HalloweenSwap.Begin(out string error))
                {
                    SelfTest.Warn(error + ": the forced states not measured");
                    yield break;
                }
                foreach (bool on in new[] { false, true })
                {
                    HalloweenSwap.Set(on);
                    SelfTest.Info($"the swap forced {(on ? "on" : "off")}: ResolveDropIconId(\"1051\") = {HalloweenHeadSkinOverride.ResolveDropIconId("1051")}, (\"1050\") = {HalloweenHeadSkinOverride.ResolveDropIconId("1050")}");
                    Draw(holder.transform, on, "carried (a copy of the keeper's overhead atom, pose 1)", overhead, true, false);
                    Draw(holder.transform, on, "on the cart (a copy of the keeper's overhead atom, pose 2, as Cargo.cs)", overhead, false, true);
                    if (ground != null) Draw(holder.transform, on, "on the ground (a copy of a ground drop's atom, pose 2)", ground, false, false);
                    yield return null;
                }
            }
            finally
            {
                HalloweenSwap.End();
                UnityEngine.Object.Destroy(holder);
            }
        }

        // One atom kind, the swap in one state: head 1050 and head 1051 on a fresh copy each, then 1051 on a copy that
        // showed 1050 first.
        private static void Draw(Transform holder, bool on, string what, DropViewAtomMesh source, bool overheadPose, bool cart)
        {
            string state = on ? "on" : "off";
            foreach (int head in new[] { 1050, 1051 })
            {
                DropViewAtomMesh view = Copy(source, holder, overheadPose);
                view.ActivateZombie(head.ToString("D4"), null);
                SelfTest.Info($"swap {state}, {what}, head {head}, fresh: {Shows(view)}{CartCheck(view, head, cart)}");
                UnityEngine.Object.Destroy(view.gameObject);
            }
            DropViewAtomMesh reused = Copy(source, holder, overheadPose);
            reused.ActivateZombie("1050", null);
            reused.ActivateZombie("1051", null);
            SelfTest.Info($"swap {state}, {what}, head 1051 after 1050: {Shows(reused)}{CartCheck(reused, 1051, cart)}");
            UnityEngine.Object.Destroy(reused.gameObject);
        }

        private static DropViewAtomMesh Copy(DropViewAtomMesh source, Transform holder, bool overheadPose)
        {
            DropViewAtomMesh view = UnityEngine.Object.Instantiate(source, holder);
            view.isOverhead = overheadPose;
            foreach (Collider c in view.GetComponentsInChildren<Collider>(includeInactive: true)) c.enabled = false;
            return view;
        }

        // The picture the view shows: its element's name and whether it is drawn (it and every parent up to the copy on).
        private static string Shows(DropViewAtomMesh view)
        {
            DropViewAtomMeshElement el = view.MeshElement;
            if (el == null) return "no picture";
            bool drawn = true;
            for (Transform t = el.transform; t != null && t != view.transform.parent; t = t.parent) drawn &= t.gameObject.activeSelf;
            return $"'{el.name}' {(drawn ? "drawn" : "NOT drawn (it or a parent off)")}";
        }

        private static string CartCheck(DropViewAtomMesh view, int head, bool cart)
        {
            if (!cart) return "";
            // the cart's own rule (Cargo.LyingZombieShown); its check by the stored head until 2026-11-04 said beside it
            bool passes = Cargo.LyingZombieShown(view);
            bool oldWarned = view.MeshElement == null || view.MeshElement.name != "drops_item_zombie_pose2_t" + head.ToString("D4");
            return (passes ? "; the cart's check passes" : "; the cart's check WARNS and draws the item's icon")
                + (passes && oldWarned ? " (its check by the stored head, until 2026-11-04, warned here)" : "");
        }

        private static string Lists(DropViewAtomMesh atom)
        {
            return "lying " + Names(DropList(atom)) + "; carried " + Names(OverheadList(atom));
        }

        private static string Names(List<DropViewAtomMeshElement> list)
        {
            if (list == null) return "(no list)";
            var names = new List<string>();
            foreach (DropViewAtomMeshElement el in list) names.Add(el != null ? el.name.Replace("drops_item_zombie_", "") : "(none)");
            return list.Count + " [" + string.Join(" ", names) + "]";
        }
    }
}
