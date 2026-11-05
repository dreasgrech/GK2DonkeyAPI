using System;
using System.Collections;
using System.Collections.Generic;
using GK2DonkeyAPI.Api;
using UnityEngine;
using static GK2DonkeyAPI.SelfTest;

namespace GK2DonkeyAPI
{
    // The mounts left in the world (MountCompanion.LeaveMountsInWorld; docs/leave-mounts.md; the user, 2026-11-05: "if im
    // riding mount a and then summon mount b from the journal, we will summon and ride mount b but leave mount a spawned at
    // that spot in the world and obviously have it persisted too"). Through the run's own companion (what the Mounts panel's
    // Ride and Drive the cart call: the Skin taken, then UseFromPanel) and a mount of the test's own (a code mount with a temp
    // pack's skin, as "mount from code" makes one), so no mod's mount is needed. Every value the tests change on the
    // companion is put back, and every mount of theirs goes, whatever happened.
    internal static class LeaveMountsTests
    {
        private const string MountOwner = Owner + ".leave";
        private const string TestMount = "selftest.leave-test";
        private const string TestSkinName = "selftest.leave-skin";
        private static string TestSkin => Looks.SkinKey(TestMount, TestSkinName);

        // The companion's values the tests change, to put back.
        private sealed class Was
        {
            internal string Skin;
            internal bool Leave, Keep;
            internal int DonkeyFoals, TestFoals;
            internal bool DonkeyFollow, TestFollow;
            internal string TestName;
        }

        private static Was Take(MountCompanion c) => new Was
        {
            Skin = c.Skin,
            Leave = c.LeaveMountsInWorld,
            Keep = c.KeepWithSaves,
            DonkeyFoals = c.OwnOf(MountCatalog.DonkeyId).FoalCount,
            TestFoals = c.OwnOf(TestMount).FoalCount,
            DonkeyFollow = c.OwnOf(MountCatalog.DonkeyId).FoalsFollowOnFoot,
            TestFollow = c.OwnOf(TestMount).FoalsFollowOnFoot,
            TestName = c.OwnOf(TestMount).Name,
        };

        private static void PutBack(MountCompanion c, Was w)
        {
            try { if (Mounts.IsMounted) Mounts.Dismount("self-test"); } catch { }
            try { c.SendAwayAll(); } catch (Exception e) { Warn("the test's mounts could not all be sent away: " + e.Message); }
            c.TestFamiliesFree = false; // held again for the rest of the run, as every companion is (MountCompanion.HeldForTests)
            c.LeaveMountsInWorld = false; // before the skin goes back: nothing is out to leave anyway
            c.Skin = w.Skin;
            c.LeaveMountsInWorld = w.Leave;
            c.KeepWithSaves = w.Keep;
            c.OwnOf(MountCatalog.DonkeyId).FoalCount = w.DonkeyFoals;
            c.OwnOf(TestMount).FoalCount = w.TestFoals;
            c.OwnOf(MountCatalog.DonkeyId).FoalsFollowOnFoot = w.DonkeyFollow;
            c.OwnOf(TestMount).FoalsFollowOnFoot = w.TestFollow;
            c.OwnOf(TestMount).Name = w.TestName;
            c.LookOf(TestSkin).Coat = CoatColor.Game;
        }

        // The test's mount: a code mount and a temp pack's skin of it (one standing picture), withdrawn by the returned
        // action. Null with a warning when it can't be made.
        private static Action MakeTestMount()
        {
            IDisposable reg = null;
            string root = null;
            try
            {
                reg = Mounts.Catalog.Register(MountOwner, new MountDef { Id = TestMount, Name = "Leave Test" });
                root = WriteTempPack("gk2donkeyapi-selftest-leave", TestSkinName, "[Skin]\nformat = 1\nname = Leave Test\nmount = " + TestMount + "\n");
                Looks.RegisterFolder(root, MountOwner);
            }
            catch (Exception e)
            {
                Warn("the test's mount could not be made: " + e.Message);
                try { reg?.Dispose(); } catch { }
                return null;
            }
            return () =>
            {
                try { Looks.Unregister(TestSkin, MountOwner); } catch { }
                try { reg.Dispose(); } catch { }
                try { if (root != null && System.IO.Directory.Exists(root)) System.IO.Directory.Delete(root, true); } catch { }
            };
        }

        private static int Babies(MountHandle parent)
        {
            if (parent == null || !parent.Exists) return -1;
            int n = 0;
            foreach (MountHandle h in parent.Children) if (h.Exists) n++;
            return n;
        }

        private static string Keys(MountHandle parent)
        {
            var k = new List<string>();
            if (parent != null && parent.Exists) foreach (MountHandle h in parent.Children) k.Add(h.Key ?? "(none)");
            k.Sort(StringComparer.Ordinal);
            return string.Join(", ", k);
        }

        private static bool FollowFlagsOff(MountHandle parent)
        {
            foreach (MountHandle h in parent.Children)
            {
                foreach (string key in new[] { "ShuffleFollowKeeperOnFoot", "ShuffleFollowKeeperIndoors" })
                {
                    if (h.Tuning.IsSet(key) && h.Tuning.SetBy(key) == Owner && h.Tuning.Get<bool>(key)) return false;
                }
            }
            return true;
        }

        private static float Apart(MountHandle a, MountHandle b)
        {
            Vector3 p = a.I.BodyPoint(), q = b.I.BodyPoint();
            return new Vector2(p.x - q.x, p.z - q.z).magnitude;
        }

        private static float FromKeeper(MountHandle a)
        {
            Vector3 p = a.I.BodyPoint(), k = Keeper();
            return new Vector2(p.x - k.x, p.z - k.z).magnitude;
        }

        // The live colliders of donkey views no mount uses round a point (the world's own donkeys among them, so a check
        // compares with the count at the start), each in 'what' with its object's path and place. The first F11 of the
        // mounts left (2026-11-05) found 6 round the keeper in the frame of the cart switch: the views the hitch and the
        // unhitch let go, their colliders and navmesh cuts back on for the pool (ReleaseView) till Destroy at the frame's
        // end; every spot of the step aside was ruled out by them. DestroyDonkey now switches a view off at once.
        private static readonly Collider[] probeHits = new Collider[64];

        private static int LooseViewColliders(Vector3 at, float radius, out string what)
        {
            int n = Physics.OverlapSphereNonAlloc(at, radius, probeHits, ~0, QueryTriggerInteraction.Ignore);
            var sb = new System.Text.StringBuilder();
            int loose = 0;
            for (int i = 0; i < n; i++)
            {
                Collider c = probeHits[i];
                if (c == null || !c) continue;
                string path = c.name;
                for (Transform p = c.transform.parent; p != null; p = p.parent) path = p.name + "/" + path;
                if (path.IndexOf("npc_donkey", StringComparison.Ordinal) < 0) continue;
                bool used = false;
                foreach (Mount.Instance m in Mount.All)
                {
                    var v = m.Donkey;
                    if (v != null && v && c.transform.IsChildOf(v.transform))
                    {
                        used = true;
                        break;
                    }
                }
                if (used) continue;
                loose++;
                sb.Append("; ").Append(path).Append(" at ").Append(c.transform.position.ToString("F2"));
            }
            what = sb.ToString();
            return loose;
        }

        // Every cart of the test's owner: free carts and mounts with one.
        private static int Carts()
        {
            int n = 0;
            foreach (MountHandle h in Mounts.InstancesOf(Owner)) if (h.Exists && h.HasCart) n++;
            return n;
        }

        // Riding the donkey, the test mount chosen as the panel's Ride does it: the donkey stays where it was with its two
        // babies (their follow switches off), out of the way of the one called; the test mount comes and is ridden. Chosen
        // again, the same donkey comes back (its id kept). The journal's number of babies, name and colours reach the one
        // left. The cart is left standing by the mount driven when another is chosen, fetched by the next, one cart all along;
        // riding a left one by hand and calling the cart takes it from the mount that has it.
        internal static IEnumerator Switch()
        {
            string notHere = WhyNotFromHere();
            if (notHere != null)
            {
                Skip(notHere);
                yield break;
            }
            Action unmake = MakeTestMount();
            if (unmake == null) yield break;
            MountCompanion c = Mounts.Companion(Owner);
            Was was = Take(c);
            try
            {
                c.SendAwayAll();
                yield return Frames(1); // the views sent away are destroyed at the frame's end
                int looseAtStart = LooseViewColliders(Keeper(), 3f, out _);
                c.TestFamiliesFree = true; // the run holds every companion's mount in place (MountCompanion.HeldForTests): this one's are the test's
                c.KeepWithSaves = false;
                c.LeaveMountsInWorld = true;
                c.Skin = "";
                c.OwnOf(MountCatalog.DonkeyId).FoalCount = 2;
                c.OwnOf(TestMount).FoalCount = 1;
                // the babies follow him on foot, so "their follow switches cleared" below has switches to clear
                c.OwnOf(MountCatalog.DonkeyId).FoalsFollowOnFoot = true;
                c.OwnOf(TestMount).FoalsFollowOnFoot = true;

                MountResult r = c.Use(MountStyle.Ride);
                Check(r.Ok, $"the donkey called to ride ({Said(r)})");
                if (!r.Ok) yield break;
                yield return Until(() => Mounts.IsMounted && c.Donkey != null && Mounts.Riding?.I == c.Donkey.I && Babies(c.Donkey) == 2, 12f);
                Check(waited, $"riding the donkey, its two babies with it ({Babies(c.Donkey)})");
                if (!waited) yield break;
                MountHandle a = c.Donkey;
                yield return Frames(2);
                Check(!FollowFlagsOff(a), "his babies have their follow switch on (the mount's own setting) before the switch");
                int aId = a.Id;

                // the test mount chosen while riding (the panel's commit), then called as Ride calls it: in the same frame, as
                // the panel does (MountsPanel.Call: Close, then UseMount); a frame between them hid a double step (pass 4)
                c.Skin = TestSkin;
                Check(a.Exists && !a.IsRidden && !Mounts.IsMounted, $"chosen: off the donkey, which is still there (exists {a.Exists}, mounted {Mounts.IsMounted})");
                Check(a.Key == MountCompanion.LeftKey(MountCatalog.DonkeyId), $"the donkey is left under its own key ({a.Key})");
                Check(c.Donkey == null && c.LeftMounts.Count == 1 && c.LeftMounts[0] == a, $"the companion has no mount out now and one left ({c.LeftMounts.Count})");
                Check(Babies(a) == 2 && Keys(a) == $"{MountCompanion.LeftFoalKey(MountCatalog.DonkeyId, 0)}, {MountCompanion.LeftFoalKey(MountCatalog.DonkeyId, 1)}",
                    $"its babies stay with it under the left keys ({Keys(a)})");
                Check(FollowFlagsOff(a), "its babies' follow switches are off: they stay with it");
                Check(!a.Tuning.IsSet("Skin") && a.Mount == MountCatalog.DonkeyId, $"it is still the donkey, not dressed in the skin chosen ({(a.Tuning.IsSet("Skin") ? a.Tuning.Get<string>("Skin") : "no skin")}, {a.Mount})");
                r = c.UseFromPanel(MountStyle.Ride);
                Check(r.Ok, $"Ride: the test mount called ({Said(r)})");
                yield return Frames(2); // the step moves its Position; the body drawn (BodyPoint) follows at the view's next stand
                float aside = FromKeeper(a);
                Check(aside >= MountCompanion.CalledMountRoom - 0.05f, $"it stepped aside for the mount called ({aside:F2} from the keeper, {MountCompanion.CalledMountRoom} wanted; the log says where)");
                // it was under the keeper: one step puts its body 1.4 to 2.3 (times its width against the donkey's) from him, a
                // second step from there twice that
                float wide = Mathf.Clamp(a.I.LooksRig.EffectScale, 1f, 2f);
                Check(aside <= 2.3f * wide + 0.6f, $"one step aside, not two ({aside:F2} from the keeper, at most {2.3f * wide + 0.6f:F2})");
                Vector3 aAt = a.Position;
                yield return Until(() => Mounts.IsMounted && c.Donkey != null && Mounts.Riding?.I == c.Donkey.I && c.Donkey.Mount == TestMount, 12f);
                Check(waited, $"riding the test mount (riding {Mounts.Riding?.FullId ?? "nothing"}, its mount {c.Donkey?.Mount})");
                if (!waited) yield break;
                MountHandle b = c.Donkey;
                Check(b != a && b.Key == MountCompanion.DonkeyKey, $"a new one, under the key \"donkey\" ({b.FullId}, {b.Key})");
                // (a standing mount may take a shuffle step meanwhile: within its area, about a unit)
                Check(a.Exists && (a.Position - aAt).sqrMagnitude < 1.5f * 1.5f && Apart(a, b) >= 1.2f, $"the donkey stands where it was left ({(a.Position - aAt).magnitude:F2} from it), not inside the one ridden ({Apart(a, b):F2} apart)");
                yield return Until(() => Babies(b) == 1, 6f);
                Check(waited && Keys(b) == MountCompanion.FoalKey, $"the test mount's own baby (its number, 1) under the key \"foal\" ({Keys(b)})");
                Check(Babies(a) == 2, $"the donkey left still has its two ({Babies(a)})");

                // the donkey chosen again: the same one comes back; the test mount is left in its turn
                c.Skin = "";
                Check(c.Donkey == a && a.Id == aId && a.Key == MountCompanion.DonkeyKey, $"chosen again: the donkey left is his again ({c.Donkey?.FullId}, key {a.Key})");
                Check(Keys(a) == $"{MountCompanion.FoalKey}, {MountCompanion.FoalKey}2", $"its babies answer to their own keys again ({Keys(a)})");
                Check(b.Exists && b.Key == MountCompanion.LeftKey(TestMount) && Keys(b) == MountCompanion.LeftFoalKey(TestMount, 0), $"the test mount is left with its baby ({b.Key}; {Keys(b)})");
                r = c.UseFromPanel(MountStyle.Ride);
                yield return Until(() => Mounts.IsMounted && Mounts.Riding?.I == a.I, 12f);
                Check(r.Ok && waited, $"Ride: the donkey came back and is ridden ({Said(r)}, riding {Mounts.Riding?.FullId ?? "nothing"})");
                // the donkey can be under him in the frame of the call (Until is then true at once), when the test mount's step
                // is not drawn yet: its body is still on the spot it was left on, his (the first F11, 2026-11-05: 0.00)
                float sameFrame = Apart(a, b);
                yield return Frames(2);
                Check(b.Exists && Apart(a, b) >= 1.2f, $"the test mount left stands apart from it ({Apart(a, b):F2}; {sameFrame:F2} in the frame the donkey came, its step not drawn yet)");

                // the journal reaches the one left: its babies' number, its name, its colours
                c.OwnOf(TestMount).FoalCount = 2;
                yield return Until(() => Babies(b) == 2, 6f);
                Check(waited && Keys(b) == $"{MountCompanion.LeftFoalKey(TestMount, 0)}, {MountCompanion.LeftFoalKey(TestMount, 1)}", $"its babies set to 2 in the journal: it has two, under the left keys ({Keys(b)})");
                Check(FollowFlagsOff(b), "and they stay with it");
                c.OwnOf(TestMount).Name = "Leftie";
                Check(b.DisplayName == "Leftie", $"its name set in the journal: \"{b.DisplayName}\"");
                c.LookOf(TestSkin).Coat = CoatColor.Brown;
                Check(b.Tuning.IsSet("CoatColorize") && b.Tuning.Get<string>("CoatColorize") == "8A5A36", $"its coat set to Brown in the journal: on it ({(b.Tuning.IsSet("CoatColorize") ? b.Tuning.Get<string>("CoatColorize") : "none")})");
                Check(c.LookOf("").Coat == CoatColor.Brown || !(a.Tuning.IsSet("CoatColorize") && a.Tuning.Get<string>("CoatColorize") == "8A5A36"), "and not on the donkey ridden (another skin's look)");

                // the cart: brought, left standing by the donkey when the test mount is chosen, fetched by it; one cart
                r = c.UseFromPanel(MountStyle.Cart);
                yield return Until(() => Mounts.IsMounted && Mounts.Style == MountStyle.Cart && a.HasCart, 12f);
                Check(r.Ok && waited, $"Drive the cart: the donkey pulls the cart ({Said(r)}, style {Mounts.Style})");
                if (waited)
                {
                    // where the donkey and the keeper are as the switch sees them (the first F11, 2026-11-05: it wasn't stepped
                    // off the cart's spot; the companion's line says why when it doesn't step)
                    Vector3 kb = PC.PhysicalBody?.Rb != null ? PC.PhysicalBody.Rb.transform.position : Keeper();
                    Info($"before the cart switch: the donkey's body at {a.I.BodyPoint():F2} ({(a.I.BodyDrawn ? "drawn" : "its root")}), its root {a.Position:F2}, "
                        + $"its cart's body {a.I.CartPoint():F2}; the keeper's body {kb:F2}, his position {Keeper():F2}; its view {(a.IsReady ? "ready" : "not ready yet")}");
                    c.Skin = TestSkin;
                    int loose = LooseViewColliders(kb, 3f, out string looseWhat);
                    Check(loose <= looseAtStart, $"in the switch's frame no view let go stands round him ({loose} collider(s) of donkey views no mount uses, {looseAtStart} at the start{looseWhat})");
                    Check(!a.HasCart && c.Cart != null && c.Cart.Exists && c.Cart.Key == MountCompanion.CartKey, $"the test mount chosen: the cart is left standing, his still ({c.Cart?.FullId}, key {c.Cart?.Key})");
                    // the unhitch on the way applies the look: the one left must keep its own skin (the new one's was put on it, 2026-11-05)
                    Check(!a.Tuning.IsSet("Skin") && a.Mount == MountCatalog.DonkeyId, $"the donkey left is still the donkey ({(a.Tuning.IsSet("Skin") ? a.Tuning.Get<string>("Skin") : "no skin")}, {a.Mount})");
                    MountHandle cart = c.Cart;
                    yield return Until(() => cart != null && cart.IsReady, 4f);
                    yield return Frames(2);
                    Vector3 body = cart != null && cart.Exists ? cart.I.CartPoint() : Vector3.zero, ab = a.I.BodyPoint();
                    float toCart = new Vector2(body.x - ab.x, body.z - ab.z).magnitude;
                    Check(cart != null && cart.Exists && toCart < 4.5f, $"by the donkey, which stepped aside ({toCart:F2} from the cart's body)");
                    Check(toCart >= 1.0f, $"the donkey not stepped onto the cart ({toCart:F2})");
                    Check(c.Donkey == b, $"the test mount left is his again ({c.Donkey?.FullId})");
                    r = c.UseFromPanel(MountStyle.Cart);
                    yield return Until(() => Mounts.IsMounted && Mounts.Riding?.I == b.I && Mounts.Style == MountStyle.Cart, 12f);
                    Check(r.Ok && waited, $"Drive the cart: the test mount came and took the cart ({Said(r)}, riding {Mounts.Riding?.FullId ?? "nothing"} in {Mounts.Style})");
                    Check(Carts() == 1, $"one cart all along ({Carts()})");

                    // the donkey left ridden by hand, the cart called: taken from the test mount standing with it
                    Mounts.Dismount("self-test");
                    yield return Until(() => !Mounts.IsMounted, 5f);
                    PutKeeper(a.Position, a.FacingRight ? 1f : -1f);
                    yield return Frames(3);
                    r = a.Ride(showMessage: false);
                    yield return Until(() => Mounts.IsMounted && Mounts.Riding?.I == a.I, 5f);
                    Check(r.Ok && waited, $"the donkey left ridden by hand ({Said(r)})");
                    if (waited)
                    {
                        r = c.Use(MountStyle.Cart);
                        yield return Until(() => a.HasCart && Mounts.Style == MountStyle.Cart, 12f);
                        Check(r.Ok && waited && !b.HasCart, $"the cart called: taken off the test mount, the donkey pulls it ({Said(r)}; the test mount has one: {b.HasCart})");
                        Check(Carts() == 1, $"still one cart ({Carts()})");
                        // chosen now, the donkey ridden with the cart becomes his where it is
                        c.Skin = "";
                        Check(c.Donkey == a && Mounts.IsMounted && Mounts.Riding?.I == a.I && a.HasCart, $"the donkey chosen while riding it: his again, the ride and the cart go on ({c.Donkey?.FullId}, riding {Mounts.IsMounted})");
                        r = c.UseFromPanel(MountStyle.Cart);
                        yield return Seconds(0.5f);
                        Check(r.Ok && Mounts.IsMounted && Mounts.Style == MountStyle.Cart && Mounts.Riding?.I == a.I, $"Drive the cart: driving on ({Said(r)})");

                        // the mounts left sent home (a player mod's button): one pulling his cart leaves it standing first
                        Mounts.Dismount("self-test");
                        yield return Until(() => !Mounts.IsMounted, 5f);
                        PutKeeper(b.Position, b.FacingRight ? 1f : -1f);
                        yield return Frames(3);
                        r = b.Ride(showMessage: false);
                        yield return Until(() => Mounts.IsMounted && Mounts.Riding?.I == b.I, 5f);
                        bool cartOnLeft = r.Ok && waited;
                        if (cartOnLeft)
                        {
                            r = c.Use(MountStyle.Cart);
                            yield return Until(() => b.HasCart && Mounts.Style == MountStyle.Cart, 12f);
                            Check(r.Ok && waited && !a.HasCart, $"the test mount left, ridden by hand, takes the cart from the donkey standing with it ({Said(r)})");
                            Mounts.Dismount("self-test");
                            yield return Until(() => !Mounts.IsMounted, 5f);
                        }
                        else
                        {
                            Warn($"the test mount left could not be ridden by hand ({Said(r)}): the send-home check runs without a cart on it");
                        }
                        int sent = c.SendLeftAway();
                        yield return Frames(2);
                        Check(sent == 1 && !b.Exists && c.LeftMounts.Count == 0 && a.Exists && c.Donkey == a, $"the mounts left sent home: the test mount went, the donkey stays his ({sent} sent, {c.LeftMounts.Count} left)");
                        if (cartOnLeft)
                        {
                            Check(Carts() == 1 && c.Cart != null && c.Cart.Exists && !c.Cart.HasDonkey && c.Cart.Key == MountCompanion.CartKey, $"the cart it pulled is left standing, his (one cart: {Carts()})");
                        }
                        else
                        {
                            Check(Carts() == 1 && a.HasCart, $"the cart still on the donkey, one cart ({Carts()})"); // it never left the donkey on this path
                        }
                    }
                }
            }
            finally
            {
                PutBack(c, was);
                unmake();
            }
            yield return Frames(3);
            Check(Mounts.InstancesOf(Owner).Count == 0 && c.LeftMounts.Count == 0, $"every mount of the test went ({Mounts.InstancesOf(Owner).Count} left)");
        }

        // Another mount chosen in the air: the ride can't end up there (the dismount lands the mount first), so the choice
        // waits; once down, the mount flown is left where it landed and the one chosen comes with the panel's call.
        internal static IEnumerator InTheAir()
        {
            if (!Flight.Active) { Skip("the flying experiment is off"); yield break; }
            string notHere = WhyNotFromHere();
            if (notHere != null) { Skip(notHere); yield break; }
            const string owner = Owner + ".leaveflier";
            const string flier = "selftest.leave-flier";
            string flierSkin = Looks.SkinKey(flier, Looks.DefaultSkinName);
            string root;
            try
            {
                root = WriteTempPack("gk2donkeyapi-selftest-leaveflier", Looks.DefaultSkinName,
                    "[Skin]\nformat = 1\nname = Leave Flier\nmount = " + flier + "\n\n[Mount]\nid = " + flier + "\nname = Leave Flier\nauthor = the self-test\nversion = 0.1\ncart = true\nfoals = true\nflies = true\n");
                Looks.RegisterFolder(root, owner);
            }
            catch (Exception e) { Warn("the temp pack could not be made: " + e.Message); yield break; }
            MountCompanion c = Mounts.Companion(Owner);
            Was was = Take(c);
            int flierFoals = c.OwnOf(flier).FoalCount;
            try
            {
                c.SendAwayAll();
                c.TestFamiliesFree = true; // the run holds every companion's mount in place (MountCompanion.HeldForTests): this one's are the test's
                c.KeepWithSaves = false;
                c.LeaveMountsInWorld = true;
                c.OwnOf(flier).FoalCount = 0;
                c.OwnOf(MountCatalog.DonkeyId).FoalCount = 0;
                c.Skin = flierSkin;
                MountResult r = c.Use(MountStyle.Ride);
                yield return Until(() => Mounts.IsMounted && c.Donkey != null && Mounts.Riding?.I == c.Donkey.I && c.Donkey.Mount == flier, 12f);
                Check(r.Ok && waited, $"riding the flier ({Said(r)}, its mount {c.Donkey?.Mount})");
                if (!waited) yield break;
                MountHandle a = c.Donkey;
                MountResult t = Mounts.TakeOff();
                if (!t.Ok) { Skip($"no take-off here ({Said(t)})"); yield break; }
                yield return Until(() => Flight.Flying && Flight.HeightPx >= 24f, 6f);
                if (!waited) { Skip($"not up in the air ({Flight.Now}, {Flight.HeightPx:0} px)"); yield break; }

                c.Skin = "";
                r = c.UseFromPanel(MountStyle.Ride);
                Check(Mounts.IsMounted && Mounts.Riding?.I == a.I && a.Key == MountCompanion.DonkeyKey && c.Donkey == a && c.Skin == flierSkin,
                    $"chosen in the air: still riding the flier, his, in its skin (riding {Mounts.Riding?.FullId ?? "nothing"}, key {a.Key}, skin \"{c.Skin}\")");
                Check(r.Ok && c.LeftMounts.Count == 0, $"Ride: waits for the landing ({Said(r)}, {c.LeftMounts.Count} left)");
                yield return Until(() => Flight.Now == Flight.Phase.Ground, 20f);
                Check(waited, $"it landed ({Flight.Now}: {Flight.Why})");
                if (!waited) yield break;
                Vector3 landed = Keeper(); // the flier is under him (a ridden one's own Position is the ride's to keep)
                yield return Until(() => Mounts.IsMounted && c.Donkey != null && c.Donkey != a && Mounts.Riding?.I == c.Donkey.I && c.Donkey.Mount == MountCatalog.DonkeyId, 12f);
                Check(waited, $"down: the donkey came and is ridden (riding {Mounts.Riding?.FullId ?? "nothing"})");
                Check(a.Exists && !a.IsRidden && a.Key == MountCompanion.LeftKey(flier) && a.Mount == flier, $"the flier is left, in its own skin ({a.Key}, {a.Mount})");
                float fromLanding = new Vector2(a.Position.x - landed.x, a.Position.z - landed.z).magnitude;
                Check(fromLanding < 2.3f * 2f + 0.5f, $"where it landed, or a step aside from it ({fromLanding:F2} from the landing spot)"); // its step aside is at most 2.3 times its drawn width (up to 2)
            }
            finally
            {
                PutBack(c, was);
                c.OwnOf(flier).FoalCount = flierFoals;
                LeftOver(owner, flierSkin, root);
            }
            yield return Frames(3);
            Check(Mounts.InstancesOf(Owner).Count == 0 && c.LeftMounts.Count == 0, $"every mount of the test went ({Mounts.InstancesOf(Owner).Count} left)");
        }

        // Off: chosen again, a mount left earlier comes back and the one out goes home (its babies too; the cart left
        // standing); with none left, the one out changes into the new mount where it is, as before. Switched off, the
        // mounts left go home, one he rides by hand once he is off it. A load: the mounts left found again by their keys; a
        // save whose mount out isn't the one chosen leaves it (on); with the option off they go home. The side file keeps
        // the left keys and the babies' parent.
        internal static IEnumerator OffAndLoad()
        {
            string notHere = WhyNotFromHere();
            if (notHere != null)
            {
                Skip(notHere);
                yield break;
            }
            Action unmake = MakeTestMount();
            if (unmake == null) yield break;
            MountCompanion c = Mounts.Companion(Owner);
            Was was = Take(c);
            try
            {
                c.SendAwayAll();
                c.TestFamiliesFree = true; // the run holds every companion's mount in place (MountCompanion.HeldForTests): this one's are the test's
                c.KeepWithSaves = false;
                c.LeaveMountsInWorld = true;
                c.Skin = "";
                c.OwnOf(MountCatalog.DonkeyId).FoalCount = 1;
                c.OwnOf(TestMount).FoalCount = 1;
                MountResult r = c.Use(MountStyle.Ride);
                yield return Until(() => Mounts.IsMounted && c.Donkey != null && Mounts.Riding?.I == c.Donkey.I && Babies(c.Donkey) == 1, 12f);
                Check(r.Ok && waited, $"riding the donkey with its baby ({Said(r)})");
                if (!waited) yield break;
                MountHandle a = c.Donkey;
                c.Skin = TestSkin;
                r = c.UseFromPanel(MountStyle.Ride);
                yield return Until(() => Mounts.IsMounted && c.Donkey != null && Mounts.Riding?.I == c.Donkey.I && c.Donkey.Mount == TestMount && Babies(c.Donkey) == 1, 12f);
                Check(r.Ok && waited && a.Exists, $"the test mount ridden, the donkey left ({Said(r)})");
                if (!waited) yield break;
                MountHandle b = c.Donkey;

                // off: the donkey chosen comes back, the test mount goes home with its baby (switched off and chosen in one
                // frame, before the tick sends a left one home: as when he rides a left one by hand and picks its mount)
                c.LeaveMountsInWorld = false;
                MountHandle bBaby = b.Children.Count > 0 ? b.Children[0] : null;
                c.Skin = "";
                Check(!b.Exists && (bBaby == null || !bBaby.Exists), $"off, the donkey chosen: the test mount went home with its baby ({b.Exists}, {bBaby?.Exists})");
                Check(c.Donkey == a && a.Key == MountCompanion.DonkeyKey && c.LeftMounts.Count == 0, $"the donkey left is his again ({c.Donkey?.FullId}; {c.LeftMounts.Count} left)");
                r = c.UseFromPanel(MountStyle.Ride);
                yield return Until(() => Mounts.IsMounted && Mounts.Riding?.I == a.I, 12f);
                Check(r.Ok && waited, $"Ride: it came and is ridden ({Said(r)})");
                // off, none left: changed where it is, as before
                c.Skin = TestSkin;
                yield return Until(() => a.Mount == TestMount, 6f);
                Check(waited && c.Donkey == a && a.Exists && Mounts.Riding?.I == a.I && c.LeftMounts.Count == 0, $"off, the test mount chosen with none left: the donkey changed into it under the rider, as before ({a.Mount}, riding {Mounts.Riding?.FullId ?? "nothing"})");

                // switched off, the mounts left go home (the user, 2026-11-05: "switching the option from the options should
                // unsummon the other mounts you're not using right now"); the one he rides stays his
                c.LeaveMountsInWorld = true;
                c.Skin = "";
                r = c.UseFromPanel(MountStyle.Ride);
                yield return Until(() => Mounts.IsMounted && c.Donkey != null && c.Donkey != a && Mounts.Riding?.I == c.Donkey.I && Babies(c.Donkey) == 1, 12f);
                Check(r.Ok && waited && a.Exists && a.Key == MountCompanion.LeftKey(TestMount), $"on again, the donkey chosen: a new one ridden, the test mount left ({Said(r)}; {a.Key})");
                if (!waited) yield break;
                MountHandle d = c.Donkey;
                yield return Until(() => Babies(a) == 1, 6f);
                MountHandle aBaby = a.Children.Count > 0 ? a.Children[0] : null;
                c.LeaveMountsInWorld = false;
                yield return Until(() => !a.Exists, 3f);
                Check(waited && (aBaby == null || !aBaby.Exists) && c.LeftMounts.Count == 0, $"switched off: the test mount left went home with its baby ({a.Exists}, {aBaby?.Exists}; {c.LeftMounts.Count} left)");
                Check(c.Donkey == d && d.Exists && Mounts.Riding?.I == d.I && Babies(d) == 1, $"and the donkey he rides stays his, with its baby (riding {Mounts.Riding?.FullId ?? "nothing"})");

                // one he rides by hand stays while he is on it, and goes once he is off it
                c.LeaveMountsInWorld = true;
                c.Skin = TestSkin;
                r = c.UseFromPanel(MountStyle.Ride);
                yield return Until(() => Mounts.IsMounted && c.Donkey != null && c.Donkey.Mount == TestMount && Mounts.Riding?.I == c.Donkey.I, 12f);
                Check(r.Ok && waited && d.Exists && d.Key == MountCompanion.LeftKey(MountCatalog.DonkeyId), $"on, the test mount chosen: the donkey left ({Said(r)}; {d.Key})");
                if (!waited) yield break;
                MountHandle e = c.Donkey;
                Mounts.Dismount("self-test");
                yield return Until(() => !Mounts.IsMounted, 5f);
                PutKeeper(d.Position, d.FacingRight ? 1f : -1f);
                yield return Frames(3);
                r = d.Ride(showMessage: false);
                yield return Until(() => Mounts.IsMounted && Mounts.Riding?.I == d.I, 5f);
                Check(r.Ok && waited, $"the donkey left ridden by hand ({Said(r)})");
                if (!waited) yield break;
                MountHandle dBaby = d.Children.Count > 0 ? d.Children[0] : null;
                c.LeaveMountsInWorld = false;
                yield return Seconds(0.75f); // three of the ticks that send a left one home
                Check(d.Exists && Mounts.Riding?.I == d.I && c.LeftMounts.Count == 1, $"switched off while he rides a left one by hand: it stays under him ({d.Exists}, riding {Mounts.Riding?.FullId ?? "nothing"})");
                Check(e.Exists && c.Donkey == e, $"and his own mount stays ({c.Donkey?.FullId})");
                Mounts.Dismount("self-test");
                yield return Until(() => !d.Exists, 3f);
                Check(waited && (dBaby == null || !dBaby.Exists) && c.LeftMounts.Count == 0, $"off it, it went home with its baby ({d.Exists}, {dBaby?.Exists})");
                a = e; // the test mount out, for the load below

                // a load: the save's mount out (the test mount, a) is not the one the cfg chose (the donkey): left (on)
                Mounts.Dismount("self-test");
                yield return Until(() => !Mounts.IsMounted, 5f);
                c.LeaveMountsInWorld = true;
                c.TestSkinQuietly("");
                c.TestFindAgain();
                Check(a.Exists && a.Key == MountCompanion.LeftKey(TestMount) && c.Donkey == null && c.LeftMounts.Count == 1, $"a load whose mount out isn't the one chosen: it is left ({a.Key}; out {c.Donkey?.FullId ?? "none"})");
                yield return Frames(2);
                Check(a.Mount == TestMount && a.Tuning.IsSet("Skin") && a.Tuning.Get<string>("Skin") == TestSkin, $"in its own skin still, not the one the cfg chose ({a.Mount})");
                c.TestFindAgain();
                Check(c.LeftMounts.Count == 1 && c.LeftMounts[0] == a && c.Donkey == null, $"found again by its key at the next load ({c.LeftMounts.Count} left)");
                yield return Until(() => Babies(a) == 1, 6f);
                Check(waited && Keys(a) == MountCompanion.LeftFoalKey(TestMount, 0), $"its baby under its left key ({Keys(a)})");

                // the side file: the left keys and the baby's parent written and read back
                c.KeepWithSaves = true;
                List<Mount.Instance> kept = Mount.KeptInstances(i => i.OwnerId == Owner, log: false);
                string text = Mount.BuildSideText("selftest", "saved at test time", 1, kept, withDormant: false, out _);
                var header = new Dictionary<string, string>();
                var nexts = new List<string[]>();
                var records = new List<Mount.Record>();
                Mount.ParseSideText(text.Split('\n'), header, nexts, records);
                Mount.Record ra = records.Find(x => x.Owner == Owner && x.Id == a.Id);
                Mount.Record rb = records.Find(x => x.Owner == Owner && x.Key == MountCompanion.LeftFoalKey(TestMount, 0));
                Check(ra != null && ra.Key == MountCompanion.LeftKey(TestMount), $"the side file keeps the left one's key ({ra?.Key ?? "no record"})");
                Check(ra != null && ra.Sets.Exists(x => x[0] == "Skin" && x[x.Length - 1] == TestSkin), "and its skin (a left one comes back in the skin it was left in)");
                Check(rb != null && rb.ParentFull == a.FullId, $"and its baby's, with its parent ({rb?.ParentFull ?? "no record"})");
                c.KeepWithSaves = false;

                // a load with the option off: the mounts left go home (off: only the mount he has is out)
                c.LeaveMountsInWorld = false;
                c.TestFindAgain();
                yield return Until(() => !a.Exists, 3f);
                Check(waited && c.LeftMounts.Count == 0, $"a load with the option off: the mount left went home ({a.Exists}; {c.LeftMounts.Count} left)");
            }
            finally
            {
                PutBack(c, was);
                unmake();
            }
            yield return Frames(3);
            Check(Mounts.InstancesOf(Owner).Count == 0 && c.LeftMounts.Count == 0, $"every mount of the test went ({Mounts.InstancesOf(Owner).Count} left)");
        }
    }
}
