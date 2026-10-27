using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using GK2DonkeyAPI.Api;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // The side file is never lost because something went wrong (docs/multi-mod-loading.md section 1): a held file left
    // as it is by a save, the last one kept as .bak, a stale one set aside instead of overwritten, a record that fails
    // to come back carried forward, a newer version's lines and keys written back, another mod's donkey not removable,
    // a mod's data capped. Everything is written into a temp folder; the player's own side file is never touched.
    internal static partial class SelfTest
    {
        internal static IEnumerator SideFileHeldAndAside()
        {
            if (Mount.SideFileHeld != null)
            {
                Warn($"the side file is held already ({Mount.SideFileHeld}): skipped, so the test can't release it");
                yield break;
            }
            string dir = Path.Combine(Path.GetTempPath(), "gk2donkeyapi-selftest-neverlost");
            MountHandle a = Spawn(home + new Vector3(3f, 0f, 0f), true, persist: true, key: "never lost");
            if (a == null)
            {
                Fail("could not spawn");
                yield break;
            }
            try
            {
                Directory.CreateDirectory(dir);
                foreach (string f in Directory.GetFiles(dir)) File.Delete(f);
                string path = Path.Combine(dir, "Slot.gk2donkeyapi.dat");
                yield return Frames(2);

                Mount.WriteSideFileAt(path, "Slot", "first save", 3);
                string first = File.Exists(path) ? File.ReadAllText(path) : "";
                Check(first.Contains("never lost") && !File.Exists(path + ".tmp"), "written: the kept donkey is in the file, no .tmp left");
                Mount.WriteSideFileAt(path, "Slot", "second save", 4);
                Check(File.Exists(path + ".bak") && File.ReadAllText(path + ".bak") == first && File.ReadAllText(path).Contains("second save"), "written again: the one before is kept as .bak");

                string before = File.ReadAllText(path);
                Info("the next \"left as it is\" warning is this test's own: the side file held");
                Mount.HoldSideFile("the self-test");
                Mount.WriteSideFileAt(path, "Slot", "third save", 5);
                Check(File.ReadAllText(path) == before, "held (an error took the donkeys away): a save leaves the file exactly as it was");
                a.SetPersist(Owner, false);
                Mount.WriteSideFileAt(path, "Slot", "fourth save", 6);
                Check(File.Exists(path) && File.ReadAllText(path) == before, "held, nothing kept any more: the file is not deleted either");
                Mount.ReleaseSideFile();
                a.SetPersist(Owner, true);

                // the file is from "second save", day 4; the slot says another time: set aside, not left to be overwritten
                Info("the next \"belongs to the save of\" warning is this test's own: a side file older than its save");
                List<MountHandle> got = Mount.RestoreFromFile(path, "Slot", "a later save", 9);
                string stale = Path.Combine(dir, "Slot.gk2donkeyapi.stale-day4.dat");
                Check(got.Count == 0 && !File.Exists(path) && File.Exists(stale) && File.ReadAllText(stale) == before,
                    $"a file older than its save: nothing restored, kept as {Path.GetFileName(stale)}");

                Check(!Mount.IsAbilityOwner("com.x.ability.tweaks") && Mount.IsAbilityOwner("acme.mod.ability.acme.crab"),
                    "an ability's owner is exactly <owner>.ability.<mount id>; a GUID holding '.ability.' is not one");
            }
            finally
            {
                Mount.ReleaseSideFile();
                if (a.Exists) a.Remove();
                try { Directory.Delete(dir, true); } catch { }
            }
        }

        internal static IEnumerator SideFileCarriedForward()
        {
            MountHandle probe = Spawn(home + new Vector3(3f, 0f, 0f), true);
            if (probe == null)
            {
                Fail("could not spawn");
                yield break;
            }
            string scene = probe.I.SceneId;
            Vector3 at = probe.I.Position;
            probe.Remove();
            string dir = Path.Combine(Path.GetTempPath(), "gk2donkeyapi-selftest-carried");
            const int okId = 9701, badId = 9702;
            var back = new List<MountHandle>();
            try
            {
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "Slot.gk2donkeyapi.dat");
                string F(float v) => v.ToString("R", CultureInfo.InvariantCulture);
                string Rec(int id, string extra) =>
                    "instance\nowner\t" + Owner + "\nid\t" + id + "\nkey\tcarried " + id + "\ndonkey\ttrue\ncart\tfalse\nscene\t" + scene +
                    "\nposition\t" + F(at.x) + "\t" + F(at.y) + "\t" + F(at.z) + "\nfacing\tright\nscale\t1\nrideable\ttrue\nremoveWithParent\ttrue\n" + extra + "end\n";
                File.WriteAllText(path, "format\t" + Mount.SideFileFormat + "\nslot\tSlot\nsaved\ttest time\nday\t7\n" +
                    Rec(okId, "set\tSomeFutureKey\t" + Owner + "\t12\nfuture\tsomething a newer version writes\n") +
                    Rec(badId, ""));
                Info("the next two warnings (\"wasn't applied\", \"could not be restored\") are this test's own: a key this version doesn't know, a restore made to fail");
                Mount.TestFailRestore = Mount.FullIdOf(Owner, badId);
                back = Mount.RestoreFromFile(path, "Slot", "test time", 7);
                Mount.TestFailRestore = null;

                MountHandle ok = back.Find(h => h.Key == "carried " + okId);
                Check(ok != null, "the good record came back");
                Mount.DormantRecord bad = null;
                foreach (Mount.DormantRecord d in Mount.Dormant) if (d.Owner == Owner && d.Id == badId) bad = d;
                Check(bad != null && bad.Why.Contains("failed"), $"the record whose restore failed is carried forward ({bad?.Why ?? "not kept"})");
                Check(!back.Exists(h => h.Key == "carried " + badId), "... and nothing of it is in the world");
                if (ok != null)
                {
                    string text = Mount.BuildSideText("selftest", "t", 1, Mount.KeptInstances(i => i == ok.I, log: false), withDormant: false, out _);
                    Check(text.Contains("SomeFutureKey") && ok.I.AsideSets.Count == 1, "a key this version doesn't know is kept as read and written back");
                    Check(text.Contains("future\tsomething a newer version writes"), "a line of a kind this version doesn't read is written back");
                }
                string all = Mount.BuildSideText("selftest", "t", 1, new List<Mount.Instance>(), withDormant: true, out _);
                Check(all.Contains("carried " + badId), "the carried record is written with the next save, as read");
                if (bad != null) Mount.ForgetDormant(bad);
            }
            finally
            {
                Mount.TestFailRestore = null;
                foreach (MountHandle h in back) if (h.Exists) h.Remove();
                try { Directory.Delete(dir, true); } catch { }
            }
        }

        internal static IEnumerator HandleOwnership()
        {
            MountHandle a = Spawn(home + new Vector3(3f, 0f, 0f), true);
            if (a == null)
            {
                Fail("could not spawn");
                yield break;
            }
            try
            {
                MountResult r = a.Remove("some.other.mod");
                Check(!r.Ok && a.Exists, $"another mod's Remove(ownerId) is refused ({r.Reason})");
                bool threw = false;
                try { a.SetData(Owner, new string('k', MountHandle.DataKeyMax + 1), "v"); } catch (ArgumentException) { threw = true; }
                Check(threw, $"a data key over {MountHandle.DataKeyMax} characters is refused");
                threw = false;
                try { a.SetData(Owner, "big", new string('v', MountHandle.DataValueMax + 1)); } catch (ArgumentException) { threw = true; }
                Check(threw && a.GetData(Owner, "big") == null, $"a data value over {MountHandle.DataValueMax} characters is refused");
                for (int k = 0; k < MountHandle.DataKeysMax; k++) a.SetData(Owner, "k" + k, "v");
                threw = false;
                try { a.SetData(Owner, "one more", "v"); } catch (ArgumentException) { threw = true; }
                a.SetData(Owner, "k0", "changed");
                Check(threw && a.GetData(Owner, "k0") == "changed", $"at {MountHandle.DataKeysMax} keys a new one is refused, a kept one still changes");
                r = a.Remove(Owner);
                Check(r.Ok && !a.Exists, "its owner's Remove(ownerId) removes it");
            }
            finally
            {
                if (a.Exists) a.Remove();
            }
        }
    }
}
