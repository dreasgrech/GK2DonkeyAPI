using System.Collections;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // The flight's landing judged by the keeper's own ground (FlightGround.WhyCannotWalkAway, docs/flying.md "Refused
    // landings: cut off from the take-off spot"): the walk from the keeper's own spot reaches his own part of the mesh at
    // once; from open ground with no part to find it ends as open ground, the same in 4 ms slices as at once, and its
    // time is read out; a pen of four temporary walls next to him walls a spot in.
    internal static partial class SelfTest
    {
        private static IEnumerator FlightWalkAway()
        {
            Rigidbody rb = PC?.PhysicalBody?.Rb;
            Collider body = PC?.PhysicalBody?.MeshCollider;
            if (rb == null || body == null || PC.SceneRecastGraph == null)
            {
                Skip("no keeper's body or no walkable mesh in this scene");
                yield break;
            }
            if (Mount.Instance.IsIndoors())
            {
                Skip("indoors: the test wants open ground round the keeper");
                yield break;
            }
            Vector3 me = rb.position;
            int mine = Mount.Instance.MeshAreaAt(me, out float off, out _);
            if (mine < 0)
            {
                Skip($"the keeper stands off the walkable mesh ({off:0.00} from it): stand on open ground");
                yield break;
            }

            // 1. his own spot: on his own part of the mesh at the first cell
            string why = Mount.Instance.WalkAway(rb, body, me, mine, out string how);
            Check(why == null, "from his own spot the walk reaches his own part of the mesh: " + how);

            // 2. the longest judgement: open ground with no part of the mesh to find, the spread runs to the open-ground
            //    size; in game it is sliced 4 ms a frame (the landing hovers meanwhile). Run once before timing it (the
            //    first run pays for the JIT), then at once and in slices: the two must agree.
            Mount.Instance.WalkAway(rb, body, me, int.MinValue, out _);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            why = Mount.Instance.WalkAway(rb, body, me, int.MinValue, out how);
            float ms = (float)watch.Elapsed.TotalMilliseconds;
            Check(why == null && how.StartsWith("open ground", System.StringComparison.Ordinal), "from his spot, with no part of the mesh to find, his ground is open ground: " + (why ?? how));
            Info($"the whole spread took {ms:0.0} ms, about {Mathf.CeilToInt(ms / Mount.Instance.WalkSliceMs)} frame(s) of hovering at {Mount.Instance.WalkSliceMs:0} ms a frame");
            Check(ms < 200f, $"the longest judgement under 200 ms in all ({ms:0.0})");
            string sliced = Mount.Instance.WalkAwaySliced(rb, body, me, int.MinValue, 0.2f, out string slicedHow, out int slices);
            Check(sliced == why && slices > 1 && Steps(slicedHow) == Steps(how), $"in slices of 0.2 ms the same answer and the same steps ({slices} slices: {sliced ?? slicedHow})");

            // 3. a pen: four walls 1.6 apart round a spot 2.5 to his side, on a layer that stops him
            int layer = LayerMask.NameToLayer("Obstacle");
            if (layer < 0 || (Mount.Instance.KeeperMask(rb) & (1 << layer)) == 0)
            {
                Info(layer < 0 ? "no layer called Obstacle: the pen not built" : "the Obstacle layer doesn't stop the keeper here: the pen not built");
                yield break;
            }
            Vector3 at = me + new Vector3(2.5f, 0f, 0f);
            float g = Mount.Instance.GroundHeight(at, out string src);
            if (float.IsNaN(g))
            {
                Info($"no ground at the pen's spot {at:F2} ({src}): the pen not built");
                yield break;
            }
            at.y = g;
            var pen = new GameObject("gk2donkeyapi-selftest-pen");
            try
            {
                pen.layer = layer;
                pen.transform.position = at; // on the ground there, not at his height (a slope)
                const float half = 0.8f, thick = 0.15f, high = 2f;
                void Wall(Vector3 centre, Vector3 size)
                {
                    var c = pen.AddComponent<BoxCollider>();
                    c.center = centre;
                    c.size = size;
                }
                Wall(new Vector3(half, high / 2f, 0f), new Vector3(thick, high, 2f * half + thick));
                Wall(new Vector3(-half, high / 2f, 0f), new Vector3(thick, high, 2f * half + thick));
                Wall(new Vector3(0f, high / 2f, half), new Vector3(2f * half + thick, high, thick));
                Wall(new Vector3(0f, high / 2f, -half), new Vector3(2f * half + thick, high, thick));
                Physics.SyncTransforms();
                why = Mount.Instance.WalkAway(rb, body, at, int.MinValue, out how);
                Check(why != null && why.StartsWith("walled in", System.StringComparison.Ordinal), "the spot inside the pen is walled in: " + (why ?? how));
                why = Mount.Instance.WalkAway(rb, body, at, mine, out how);
                Info("inside the pen, to his own part of the mesh (the NPCs' mesh doesn't know the pen; a cell on his part is a yes): " + (why ?? how));
            }
            finally
            {
                // gone for the tests after this one at once (Destroy waits for the frame's end)
                pen.SetActive(false);
                Object.Destroy(pen);
                Physics.SyncTransforms();
            }
        }

        // "... (536 steps, 18.7 ms over 1 frame(s))" -> 536
        private static int Steps(string how)
        {
            if (how == null) return -1;
            int at = how.LastIndexOf(" steps, ", System.StringComparison.Ordinal);
            if (at <= 0) return -1;
            int from = how.LastIndexOf('(', at);
            return from >= 0 && int.TryParse(how.Substring(from + 1, at - from - 1), out int n) ? n : -1;
        }
    }
}
