using HarmonyLib;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // The arrow's origin while mounted (docs/mounted-combat.md §10.6). The weapons are held at their rest height over
    // the physical body so that the sword fan meets the enemies' colliders; the drawn bow sits the seat higher, so the
    // arrow appeared from where the keeper would stand on foot. Projectile.Launch takes the held Bow's position; this
    // postfix (the keeper's arrows while mounted only) lifts the arrow to the drawn bow and it flies level from there.
    // Measured 2026-10-14 (Dev/ZoneProbe, since removed): the enemies' hit capsules run from 0.04 to 1.44 over the
    // ground and the arrow's sweep box is 0.034 high; the drawn bow is at 1.00, the held one at 0.78: both inside the
    // band, so the lifted arrow hits exactly as before. Projectile.Tick adds direction × speed to the transform each
    // Update, so the lifted position is kept.
    [HarmonyPatch(typeof(Projectile), nameof(Projectile.Launch), typeof(ProjectileStats), typeof(Vector3), typeof(Vector3))]
    internal static class Projectile_Launch_Patch
    {
        private static bool logged;

        private static void Postfix(Projectile __instance, Vector3 position)
        {
            if (!Mount.IsMounted || !(__instance is ArrowProjectile))
            {
                return;
            }
            float lift = Mount.Ridden != null ? Mount.Ridden.ArrowLiftAt(position) : 0f;
            if (lift <= 0.001f)
            {
                return;
            }
            Vector3 p = __instance.transform.position;
            p.y += lift;
            __instance.transform.position = p;
            if (!logged)
            {
                logged = true;
                Plugin.Log.LogInfo($"Mount: the arrow leaves the drawn bow at y {p.y:F2} (the held bow's {position.y:F2} plus the seat's {lift:F2}) and flies level.");
            }
        }
    }
}
