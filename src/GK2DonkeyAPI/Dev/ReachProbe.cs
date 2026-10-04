using System.Text;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // Investigation (Debug tab): what the keeper-move gate (SafeGround.cs, WhyKeeperCannotReach) says around the keeper,
    // before trusting it with a ride. 16 directions at 1.0 and 1.8 units, each spot at the ground's height there: one
    // [reach] line per spot, "may move" or the reason it may not. Stand next to a rock, a fence or a wall and the spots on
    // its far side must all be refused (docs/safe-ground.md, "Locked in behind a rock").
    internal static class ReachProbe
    {
        internal static void Write()
        {
            PlayerController pc = MainGame.PlayerController;
            Rigidbody rb = pc?.PhysicalBody?.Rb;
            if (rb == null)
            {
                Plugin.Log.LogInfo("[reach] no keeper.");
                return;
            }
            Vector3 k = rb.position;
            int ok = 0, refused = 0;
            var sb = new StringBuilder();
            foreach (float r in new[] { 1.0f, 1.8f })
            {
                for (int i = 0; i < 16; i++)
                {
                    float a = i * Mathf.PI / 8f;
                    Vector3 p = k + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                    float g = Mount.Instance.GroundHeight(p, out string src);
                    if (float.IsNaN(g))
                    {
                        sb.AppendLine($"[reach] {r:F1} at {i * 22.5f:F0}°: {p:F2} no ground ({src})");
                        refused++;
                        continue;
                    }
                    p.y = g;
                    string why = Mount.Instance.WhyKeeperCannotReach(pc, k, p);
                    if (why == null) ok++; else refused++;
                    sb.AppendLine($"[reach] {r:F1} at {i * 22.5f:F0}°: {p:F2} ({src}) {(why == null ? "may move" : "refused: " + why)}");
                }
            }
            Plugin.Log.LogInfo($"[reach] from the keeper at {k:F2} in {pc.PlayerData?.currentGameSceneId}: {ok} spot(s) allowed, {refused} refused (0° = +x, 90° = +z, away from the camera).\n{sb}");
        }
    }
}
