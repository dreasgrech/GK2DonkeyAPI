using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // The self-test of the donkey-art check (DonkeyFrames; docs/skins-design.md section 11): the comparison on made-up
    // lists, the fingerprint of a known picture against the one tools/skins/donkey_frames.py gives it (the two sides hash
    // the same way), the list in the DLL, and the session's own check of the game's frames.
    internal static class DonkeyFramesTests
    {
        private static DonkeyFrames.Print P(int w, int h, ulong hash) => new DonkeyFrames.Print { W = w, H = h, Hash = hash };

        internal static IEnumerator Check()
        {
            var want = new Dictionary<string, DonkeyFrames.Print> { { "a", P(96, 96, 1) }, { "b", P(96, 96, 2) }, { "c", P(96, 96, 3) }, { "e", P(96, 96, 5) } };
            var have = new Dictionary<string, DonkeyFrames.Print> { { "a", P(96, 96, 1) }, { "b", P(96, 96, 9) }, { "d", P(96, 96, 4) }, { "e", P(98, 96, 5) } };
            var changed = new List<string>();
            var missing = new List<string>();
            var extra = new List<string>();
            DonkeyFrames.Compare(want, have, changed, missing, extra);
            SelfTest.Check(string.Join(",", changed.ToArray()) == "b,e", $"the comparison: a frame whose pixels or canvas differ is changed (b, e): {string.Join(",", changed.ToArray())}");
            SelfTest.Check(string.Join(",", missing.ToArray()) == "c", $"the comparison: a listed frame the game lacks is missing (c): {string.Join(",", missing.ToArray())}");
            SelfTest.Check(string.Join(",", extra.ToArray()) == "d", $"the comparison: a frame the list lacks is new (d): {string.Join(",", extra.ToArray())}");

            // rows from the top: red, transparent (its colour not counted); half-transparent green, (1, 2, 3, 4). Unity's
            // pixels run from the bottom row. donkey_frames.py's fnv() of those bytes: ce96f3c3641e1e06.
            var px = new[] { new Color32(0, 255, 0, 128), new Color32(1, 2, 3, 4), new Color32(255, 0, 0, 255), new Color32(10, 20, 30, 0) };
            DonkeyFrames.Print known = DonkeyFrames.Of(2, 2, px);
            SelfTest.Check(known.Hash == 0xce96f3c3641e1e06UL, $"the fingerprint of a known 2x2 picture is the one donkey_frames.py gives it (ce96f3c3641e1e06): {known.Hash:x16}");
            Dictionary<string, DonkeyFrames.Print> parsed = DonkeyFrames.ParseList("# a comment\nbdy_a 96x96 00000000000000ff\r\nbroken line\nbdy_b 2x3 ce96f3c3641e1e06\n");
            SelfTest.Check(parsed.Count == 2 && parsed["bdy_a"].Hash == 0xffUL && parsed["bdy_b"].W == 2 && parsed["bdy_b"].H == 3,
                $"the list's lines read (comments and a broken line skipped, CRLF or LF): {parsed.Count} frame(s)");

            // the session's check: run already, or running now that a donkey is out
            yield return SelfTest.Until(() => DonkeyFrames.Now == DonkeyFrames.State.Same || DonkeyFrames.Now == DonkeyFrames.State.Differs || DonkeyFrames.Now == DonkeyFrames.State.NoList || DonkeyFrames.Now == DonkeyFrames.State.NotFound, 40f);
            if (DonkeyFrames.Now != DonkeyFrames.State.Same && DonkeyFrames.Now != DonkeyFrames.State.Differs && DonkeyFrames.Now != DonkeyFrames.State.NoList)
            {
                SelfTest.Skip($"the game's donkey frames were not read within 40 s ({DonkeyFrames.Now}{(DonkeyFrames.Note.Length > 0 ? ": " + DonkeyFrames.Note : "")}; no mount out in the world?)");
                yield break;
            }
            SelfTest.Check(DonkeyFrames.Now == DonkeyFrames.State.Same, DonkeyFrames.Now == DonkeyFrames.State.NoList ? "the list is in the DLL" : DonkeyFrames.Note);
        }
    }
}
