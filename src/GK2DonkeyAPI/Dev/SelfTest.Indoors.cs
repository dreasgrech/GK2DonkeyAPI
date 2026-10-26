using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using GK2DonkeyAPI.Api;

namespace GK2DonkeyAPI
{
    // Mounting indoors (a player asked, 2026-10-21; docs/indoors.md): the API's DismountIndoors and CartIndoors, Saddle
    // Up's "Allow mounting indoors" and "Allow mounting with the cart indoors". Two scenarios: the rules under the
    // game's indoor light (every refusal and every end of a ride, all three settings), and a real door: the game's own
    // PlayerController.Teleport (the fade, control taken, SetPosition, the indoor preset, control back) into a building
    // of this scene, riding and driving, and back out.
    internal static partial class SelfTest
    {
        private const string IndoorPreset = "indoor"; // the game's houses (its log: "SetTimeOfDayPreset: [indoor]")

        // The two switches taken over from whoever holds them (Saddle Up), given back in the finally.
        private sealed class IndoorSwitches
        {
            private readonly string rideOwner, cartOwner;
            private readonly object rideWas, cartWas;

            internal IndoorSwitches()
            {
                rideOwner = Mounts.Behaviour.OverriddenBy("DismountIndoors");
                rideWas = rideOwner == null ? null : Mounts.Behaviour.Get<object>("DismountIndoors");
                cartOwner = Mounts.Behaviour.OverriddenBy("CartIndoors");
                cartWas = cartOwner == null ? null : Mounts.Behaviour.Get<object>("CartIndoors");
            }

            internal void Set(bool rideIndoors, bool cartIndoors)
            {
                Mounts.Behaviour.ClearAny("DismountIndoors");
                Mounts.Behaviour.ClearAny("CartIndoors");
                Mounts.Behaviour.Set(Owner, "DismountIndoors", !rideIndoors);
                Mounts.Behaviour.Set(Owner, "CartIndoors", cartIndoors);
            }

            internal void Restore()
            {
                Mounts.Behaviour.ClearAny("DismountIndoors");
                Mounts.Behaviour.ClearAny("CartIndoors");
                if (rideOwner != null && rideWas != null) Mounts.Behaviour.Set(rideOwner, "DismountIndoors", rideWas);
                if (cartOwner != null && cartWas != null) Mounts.Behaviour.Set(cartOwner, "CartIndoors", cartWas);
            }
        }

        // The light preset where the run started ("outdoor", or the area's own): put back by its name, which the
        // game writes into the save (EnvironmentData.timeOfDayPresetName), never a guessed "outdoor".
        private static string startPreset;

        // Why these scenarios don't run from here (null: they do), with the start's preset read.
        private static string WhyNotFromHere()
        {
            if (Mount.Instance.IsCoop()) return "a co-op game: the light preset is the other player's too";
            if (!PC.IsControlsEnabled || !(PC.Ssm?.CurState is FreePlayerState)) return "the keeper is busy (a script, a conversation, work): run the tests where he walks freely";
            if (Mount.Instance.IsIndoors()) return "the keeper is indoors (a building, the church, a dungeon): run the tests from outside";
            startPreset = EnvironmentEngine.Instance?.TimesOfDay?.name;
            if (string.IsNullOrEmpty(startPreset)) return "the game's light preset can't be read here";
            // both names must be ones SetTimeOfDayPreset knows, or the light couldn't be put back (it looks them up by name)
            var presets = AccessTools.Field(typeof(EnvironmentEngine), "availablePresets")?.GetValue(EnvironmentEngine.Instance) as System.Collections.IDictionary;
            if (presets == null || !presets.Contains(startPreset) || !presets.Contains(IndoorPreset))
                return $"the light presets \"{startPreset}\" and \"{IndoorPreset}\" aren't both known to the game: the light couldn't be put back";
            if (Mount.Instance.InArmour(PC)) return "the keeper is in armour (a fight): run the tests outside a fight";
            return null;
        }

        private static IEnumerator LightIndoors(bool indoors)
        {
            EnvironmentEngine.Instance.SetTimeOfDayPreset(indoors ? IndoorPreset : startPreset);
            yield return Until(() => Mount.Instance.IsIndoors() == indoors, 3f);
            yield return Frames(2); // the ride's tick has read it
        }

        private static string Said(MountResult r) => r.Ok ? "ok" : r.Reason;

        // The rules: refusals and ends of a ride with each setting, under the game's indoor light where the keeper stands.
        private static IEnumerator IndoorsRules()
        {
            string notHere = WhyNotFromHere();
            if (notHere != null)
            {
                Skip(notHere);
                yield break;
            }
            // Saddle Up's two rows (read, not changed: they are the player's): the second greys while the first is off
            MountSettingsPage saddle = MountSettingsPage.All.Find(pg => pg.OwnerId == "dreas.gk2.saddleup" && pg.Shown);
            if (saddle != null)
            {
                MountSettingsPage.Row ride = saddle.Rows.Find(x => x.Entry != null && x.Entry.Definition.Key == "MountIndoors");
                MountSettingsPage.Row cartRow = saddle.Rows.Find(x => x.Entry != null && x.Entry.Definition.Key == "CartIndoors");
                bool rideOn = ride?.Entry is BepInEx.Configuration.ConfigEntry<bool> re && re.Value;
                Check(ride != null && cartRow != null && cartRow.Enabled != null && cartRow.Enabled() == rideOn,
                    $"Saddle Up's rows: \"Allow mounting indoors\" ({(ride == null ? "missing" : rideOn ? "on" : "off")}) and \"... with the cart\" ({(cartRow == null ? "missing" : cartRow.Enabled != null && cartRow.Enabled() ? "live" : "greyed")})");
                Check(Mounts.Behaviour.Get<bool>("DismountIndoors") == !rideOn, $"the first row reaches the API (DismountIndoors {Mounts.Behaviour.Get<bool>("DismountIndoors")})");
            }
            Vector3 near = Spot(home, 3f) ?? home;
            MountHandle plain = Spawn(near, right: homeFacing > 0f);
            MountHandle carted = Spawn(near + new Vector3(0f, 0f, 2.5f), right: homeFacing > 0f, withCart: true);
            MountHandle freeCart = Spawn(near + new Vector3(3f, 0f, 0f), right: homeFacing > 0f, donkey: false);
            MountHandle carted2 = Spawn(near + new Vector3(3f, 0f, 2.5f), right: homeFacing > 0f, withCart: true);
            if (plain == null || carted == null || freeCart == null || carted2 == null)
            {
                Fail("could not spawn");
                yield break;
            }
            var sw = new IndoorSwitches();
            string reason = null;
            Action<string> onDismounted = r => reason = r;
            Mounts.Dismounted += onDismounted;
            bool lit = false;
            const string NoCart = Mount.Instance.NotTheCartIndoors;
            try
            {
                yield return Until(() => plain.IsReady && carted.IsReady && freeCart.IsReady && carted2.IsReady, 6f);

                // 1. both off (Saddle Up's default, as before 0.36.0)
                sw.Set(false, false);
                MountResult r = plain.Ride(showMessage: false);
                Check(r.Ok, $"outdoors: ridden ({Said(r)})");
                if (!r.Ok) yield break; // the light is never changed for a ride that didn't happen
                reason = null;
                lit = true;
                yield return LightIndoors(true);
                if (!Mount.Instance.IsIndoors())
                {
                    Warn($"the game's \"{IndoorPreset}\" light preset didn't apply: skipped");
                    yield break;
                }
                Check(!Mounts.IsMounted && reason == "indoors", $"both off: the indoor light ends the ride (\"{reason ?? "still riding"}\")");
                r = plain.Ride(showMessage: false);
                Check(!r.Ok && r.Reason == "Not indoors.", $"both off: riding refused indoors ({Said(r)})");
                r = carted.Ride(showMessage: false);
                Check(!r.Ok && r.Reason == "Not indoors.", $"both off: the cart refused indoors ({Said(r)})");

                // 2. riding indoors, the cart not
                sw.Set(true, false);
                r = plain.Ride(showMessage: false);
                Check(r.Ok, $"riding allowed: ridden indoors ({Said(r)})");
                if (r.Ok)
                {
                    yield return Frames(3);
                    MountResult s = Mounts.SwitchStyle(MountStyle.Cart, showMessage: false);
                    Check(!s.Ok && s.Reason == NoCart, $"riding allowed: the switch to the cart refused indoors ({Said(s)})");
                    MountResult hi = Mounts.Hitch(plain, freeCart);
                    Check(!hi.Ok && hi.Reason == NoCart, $"riding allowed: a hitch from the saddle refused indoors ({Said(hi)})");
                    Check(Mounts.IsMounted && Mounts.Style == MountStyle.Ride, "still riding, without a cart");
                    Mounts.Dismount("self-test");
                    yield return Frames(2);
                }
                r = carted.Ride(showMessage: false);
                Check(!r.Ok && r.Reason == NoCart, $"riding allowed: a mount with its cart refused indoors ({Said(r)})");
                MountResult foot = Mounts.Hitch(plain, freeCart);
                Check(!foot.Ok && foot.Reason == NoCart, $"riding allowed: a hitch on foot refused indoors ({Said(foot)})");

                yield return LightIndoors(false);
                r = plain.Ride(showMessage: false);
                Check(r.Ok, $"outdoors: ridden ({Said(r)})");
                reason = null;
                yield return LightIndoors(true);
                yield return Frames(4);
                Check(Mounts.IsMounted && reason == null, $"riding allowed: the ride goes on into the indoor light ({reason ?? "riding"})");
                sw.Set(false, false);
                yield return Frames(3);
                Check(!Mounts.IsMounted && reason == "indoors", $"riding switched off inside: the ride ends (\"{reason ?? "still riding"}\")");

                sw.Set(true, false);
                yield return LightIndoors(false);
                r = carted.Ride(showMessage: false);
                Check(r.Ok && Mounts.Style == MountStyle.Cart, $"outdoors: the cart driven ({Said(r)})");
                reason = null;
                Vector3? stood = carted.I.TestCartBodyWorld();
                var had = Carts();
                yield return LightIndoors(true);
                yield return Frames(3);
                MountHandle left = NewCart(had);
                Check(Mounts.IsMounted && Mounts.Style == MountStyle.Ride && reason == null && !carted.HasCart, $"riding allowed, the cart not: the drive goes on as a ride, without the cart ({(Mounts.IsMounted ? Mounts.Style.ToString() : "off: " + reason)})");
                Check(left != null && stood != null && Flat(left.I.Position, stood.Value) < 1.5f, $"the cart stays standing where it was ({(left == null ? "none left" : left.I.Position.ToString("F1"))}, it stood at {(stood == null ? "?" : stood.Value.ToString("F1"))})");
                Mounts.Dismount("self-test");
                yield return Frames(2);

                // 3. both on
                sw.Set(true, true);
                r = carted2.Ride(showMessage: false);
                Check(r.Ok && Mounts.Style == MountStyle.Cart, $"both on: the cart driven indoors ({Said(r)})");
                reason = null;
                yield return Frames(5);
                Check(Mounts.IsMounted && reason == null, $"both on: the drive goes on ({reason ?? "driving"})");
                sw.Set(true, false);
                yield return Frames(3);
                Check(Mounts.IsMounted && Mounts.Style == MountStyle.Ride && !carted2.HasCart, $"the cart switched off inside: the cart is left, the ride goes on ({(Mounts.IsMounted ? Mounts.Style.ToString() : "off: " + reason)})");
            }
            finally
            {
                Mounts.Dismounted -= onDismounted;
                if (Mounts.IsMounted) Mounts.Dismount("self-test");
                sw.Restore();
                if (lit) EnvironmentEngine.Instance.SetTimeOfDayPreset(startPreset); // the start's own, by name
                foreach (MountHandle h in Mounts.InstancesOf(Owner)) if (h.Exists) h.Remove(); // the carts left standing too
            }
        }

        // The test's free carts now, and the one that appeared since (a cart left standing).
        private static HashSet<string> Carts()
        {
            var s = new HashSet<string>();
            foreach (MountHandle h in Mounts.InstancesOf(Owner)) if (h.Exists && !h.I.HasDonkey && h.HasCart) s.Add(h.FullId);
            return s;
        }

        private static MountHandle NewCart(HashSet<string> had)
        {
            foreach (MountHandle h in Mounts.InstancesOf(Owner)) if (h.Exists && !h.I.HasDonkey && h.HasCart && !had.Contains(h.FullId)) return h;
            return null;
        }

        // ------------------------------------------------------------------ a real door

        // A door as the game's are (WgoTeleportData, GDPointTeleportData): a point and a light preset. No scene data:
        // the game's same-scene path (a house's inside is elsewhere in the same scene). Its id is heard by nothing
        // (the game's teleport triggers fire only for ids a quest waits for).
        private sealed class SelfTestDoor : TeleportDataBase
        {
            private readonly Vector3 to;

            internal SelfTestDoor(Vector3 to, string preset) : base(preset)
            {
                this.to = to;
            }

            public override string GetDestinationId() => "gk2donkeyapi-selftest-door";

            public override GameSceneData GetDestinationSceneData() => null;

            public override Vector3 GetPosition() => to;
        }

        private static bool doorOk;

        private static IEnumerator ThroughDoor(Vector3 to, bool indoors, bool riddenFoals = false)
        {
            doorOk = false;
            Mount.TestDoorQuiet = true; // the player's own foals stay put, the morgue's door note isn't taken
            Mount.TestDoorRiddenFoalsOnly = riddenFoals; // but the foals of the mount ridden through it come along
            try
            {
                if (!PlayerController.Teleport(new SelfTestDoor(to, indoors ? IndoorPreset : startPreset)))
                {
                    yield break;
                }
                yield return ControlBack();
                yield return Frames(5);
            }
            finally
            {
                Mount.TestDoorQuiet = false;
                Mount.TestDoorRiddenFoalsOnly = false;
            }
            doorOk = PC.IsControlsEnabled && Mount.Instance.IsIndoors() == indoors && Flat(Keeper(), to) < 1.5f;
        }

        // The door's fade and control back, at most 12 s; not cut short by Abort (a second teleport while one runs
        // would leave the keeper between the two).
        private static IEnumerator ControlBack()
        {
            float end = Time.unscaledTime + 1f;
            while (PC.IsControlsEnabled && Time.unscaledTime < end) yield return null; // taken (ByTeleport) at once, normally
            end = Time.unscaledTime + 12f;
            while (!PC.IsControlsEnabled && Time.unscaledTime < end) yield return null;
        }

        private static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        private static readonly RaycastHit[] zoneHits = new RaycastHit[16];

        // Why standing at p would tell the game something (null: nothing), as its own zone check would read it there
        // (PlayerPhysicalBody.UpdateWorldZone: world-zone colliders, layer 17, from 50 above): a zone the player hasn't
        // visited (the save's list would gain it), or one a quest waits to be walked into (WalkIntoWorldZone).
        private static string ZoneNotQuiet(Vector3 p)
        {
            List<string> visited = MainGame.Instance?.GameSave?.knowledgeSystem?.visitedWorldZones;
            System.Collections.IDictionary walkInto = null;
            try
            {
                object events = MainGame.Instance?.GameSave?.globalEventsSystem;
                var byType = events == null ? null : AccessTools.Field(typeof(GlobalEventsSystem), "eventsCache")?.GetValue(events) as System.Collections.IDictionary;
                if (byType == null) return "the game's quest triggers can't be read";
                walkInto = byType.Contains(GlobalEventsSystem.Event.Type.WalkIntoWorldZone) ? byType[GlobalEventsSystem.Event.Type.WalkIntoWorldZone] as System.Collections.IDictionary : null;
            }
            catch (Exception e)
            {
                return "the game's quest triggers can't be read (" + e.Message + ")";
            }
            int n = Physics.RaycastNonAlloc(new Ray(p + Vector3.up * 50f, Vector3.down), zoneHits, 100f, 131072);
            for (int k = 0; k < n; k++)
            {
                Collider c = zoneHits[k].collider;
                if (c == null) continue;
                if (!c.TryGetComponent(out WorldZone wz) && (c.transform.parent == null || !c.transform.parent.TryGetComponent(out wz))) continue;
                string dataId = wz.Data?.id ?? wz.Id;
                if (walkInto != null && (walkInto.Contains(dataId) || walkInto.Contains(wz.Id))) return $"a quest waits for the keeper to walk into {dataId}";
                if (visited == null || !visited.Contains(wz.Id)) return $"{wz.Id} is not visited yet";
            }
            return null;
        }

        // A floor inside one of this scene's buildings (the game's IndoorArea boxes, kept under a switched-off
        // collection: read from their colliders' own geometry) on the keeper's walkable floor with room to stand, where
        // landing tells the game nothing (ZoneNotQuiet), the nearest to the keeper.
        private static Vector3? InsideABuilding(out string area, out string why)
        {
            area = null;
            why = null;
            GameScene gs = Mount.LoadedScene(MainGame.PlayerData?.currentGameSceneId);
            Pathfinding.RecastGraph graph = PC?.SceneRecastGraph;
            if (gs == null || graph == null)
            {
                why = "the scene or its walkable floor can't be read";
                return null;
            }
            var found = new List<(Vector3 At, string Id)>();
            int buildings = 0;
            foreach (IndoorArea a in Resources.FindObjectsOfTypeAll<IndoorArea>())
            {
                if (a == null || !a.gameObject.scene.IsValid() || a.gameObject.scene != gs.gameObject.scene || a.Colliders == null)
                {
                    continue;
                }
                buildings++;
                foreach (BoxCollider b in a.Colliders)
                {
                    if (b == null) continue;
                    Vector3 c = b.transform.TransformPoint(b.center);
                    Pathfinding.NNInfo nn = graph.GetNearest(c, Pathfinding.NearestNodeConstraint.Walkable); // the keeper's own walkable floor
                    if (nn.node == null || (nn.position - c).magnitude > 6f || !InBoxXZ(b, nn.position) || !FreeSpot(nn.position))
                    {
                        continue;
                    }
                    string loud = ZoneNotQuiet(nn.position);
                    if (loud != null)
                    {
                        why = why ?? loud; // the first reason, for the skip line
                        continue;
                    }
                    found.Add((nn.position, a.Id));
                }
            }
            if (found.Count == 0)
            {
                why = buildings == 0 ? "no building in this scene" : $"{buildings} building(s), none to stand in quietly ({why ?? "no floor with room"})";
                return null;
            }
            Vector3 k = Keeper();
            found.Sort((x, y) => Flat(x.At, k).CompareTo(Flat(y.At, k)));
            area = found[0].Id;
            return found[0].At;
        }

        // Inside the box seen from above, from the collider's own geometry (its bounds are empty while it is switched
        // off, as the game keeps them), a margin in from the walls; as IndoorAreaBoundData.ContainsXZ.
        private static bool InBoxXZ(BoxCollider b, Vector3 p, float margin = 0.75f)
        {
            Vector3 local = b.transform.InverseTransformPoint(p) - b.center;
            Vector3 half = b.size * 0.5f;
            Vector3 scale = b.transform.lossyScale;
            float mx = margin / Mathf.Max(0.001f, Mathf.Abs(scale.x)), mz = margin / Mathf.Max(0.001f, Mathf.Abs(scale.z));
            return Mathf.Abs(local.x) <= half.x - mx && Mathf.Abs(local.z) <= half.z - mz;
        }

        // Whether p is inside any of the scene's buildings (its IndoorArea boxes, to the walls).
        private static bool InAnyBuilding(Vector3 p, GameScene gs)
        {
            if (gs == null) return false;
            foreach (IndoorArea a in Resources.FindObjectsOfTypeAll<IndoorArea>())
            {
                if (a == null || !a.gameObject.scene.IsValid() || a.gameObject.scene != gs.gameObject.scene || a.Colliders == null) continue;
                foreach (BoxCollider b in a.Colliders)
                {
                    if (b != null && InBoxXZ(b, p, 0f)) return true;
                }
            }
            return false;
        }

        // Through a building's door riding, then driving with the cart refused, then with it allowed, and back out each
        // time: where the mount and the cart are, on which floor. Whatever happens inside, the keeper is brought back to
        // where he started, by a door, once control is his again.
        private static IEnumerator IndoorsThroughADoor()
        {
            string notHere = WhyNotFromHere();
            if (notHere != null)
            {
                Skip(notHere);
                yield break;
            }
            Vector3 start = Keeper();
            if (InAnyBuilding(start, Mount.LoadedScene(MainGame.PlayerData?.currentGameSceneId)))
            {
                Skip("the run stands in a building (no spot outside one in this scene): there is no outside to come back to");
                yield break;
            }
            string startLoud = ZoneNotQuiet(start);
            if (startLoud != null)
            {
                Skip("coming back out here would tell the game something: " + startLoud);
                yield break;
            }
            Vector3? inside = InsideABuilding(out string area, out string noBuilding);
            if (inside == null)
            {
                Skip(noBuilding);
                yield break;
            }
            Info($"the building: {area}, inside at {inside.Value:F1}");
            var sw = new IndoorSwitches();
            try
            {
                yield return DoorRounds(inside.Value, sw);
            }
            finally
            {
                if (Mounts.IsMounted) Mounts.Dismount("self-test");
                sw.Restore();
                foreach (MountHandle h in Mounts.InstancesOf(Owner)) if (h.Exists) h.Remove();
            }
            // never left inside, nor anywhere else the rounds took him
            yield return ControlBack();
            if (Mount.Instance.IsIndoors() || Flat(Keeper(), start) > 1.5f)
            {
                yield return ThroughDoor(start, indoors: false);
                if (!doorOk) Fail($"the keeper could not be brought back out to {start:F1} (he is at {Keeper():F1}, indoors {Mount.Instance.IsIndoors()})");
            }
            if (EnvironmentEngine.Instance?.TimesOfDay?.name != startPreset)
            {
                EnvironmentEngine.Instance.SetTimeOfDayPreset(startPreset);
            }
        }

        private static IEnumerator DoorRounds(Vector3 inside, IndoorSwitches sw)
        {
            Vector3 near = Spot(home, 3f) ?? home;
            MountHandle plain = Spawn(near, right: homeFacing > 0f);
            MountHandle carted = Spawn(near + new Vector3(0f, 0f, 2.5f), right: homeFacing > 0f, withCart: true);
            MountHandle carted2 = Spawn(near + new Vector3(3f, 0f, 2.5f), right: homeFacing > 0f, withCart: true);
            if (plain == null || carted == null || carted2 == null)
            {
                Fail("could not spawn");
                yield break;
            }
            string reason = null;
            Action<string> onDismounted = r => reason = r;
            Mounts.Dismounted += onDismounted;
            try
            {
                yield return Until(() => plain.IsReady && carted.IsReady && carted2.IsReady, 6f);

                // with the mount ridden: is it by the keeper, on his floor
                string Beside(MountHandle h)
                {
                    Vector3 k = Keeper(), p = h.I.Position;
                    return Flat(k, p) < 2.5f && Mathf.Abs(k.y - p.y) < 0.6f ? null : $"the mount at {p:F1}, the keeper at {k:F1}";
                }

                // riding allowed, the cart not: riding in and out, with a foal of the ridden mount, which comes along
                // through both doors (the user, 2026-10-24: Gerry's babies stayed in the house when he was ridden out)
                MountHandle baby = Mounts.SpawnFoal(Owner, plain, 0.6f, persist: false);
                if (baby != null) yield return Until(() => baby.IsReady, 6f);
                string ByParent()
                {
                    if (baby == null || !baby.Exists) return "no foal";
                    Vector3 f = baby.Position, m = plain.I.Position;
                    return Flat(f, m) < 4f && Mathf.Abs(f.y - m.y) < 0.6f ? null : $"the foal at {f:F1}, its parent at {m:F1}";
                }
                sw.Set(true, false);
                MountResult r = plain.Ride(showMessage: false);
                Check(r.Ok, $"ridden ({Said(r)})");
                if (!r.Ok) yield break;
                yield return Frames(3);
                Vector3 outside = Keeper();
                reason = null;
                yield return ThroughDoor(inside, indoors: true, riddenFoals: true);
                if (!doorOk)
                {
                    Warn($"the door didn't take the keeper in (he is at {Keeper():F1}, indoors {Mount.Instance.IsIndoors()}): skipped");
                    yield break;
                }
                Check(Mounts.IsMounted && Mounts.Style == MountStyle.Ride, $"riding through the door: still riding inside ({reason ?? "riding"})");
                string off = Mounts.IsMounted ? Beside(plain) : "not ridden";
                Check(off == null, $"inside, the mount is under the keeper, on the building's floor ({off ?? "yes"})");
                if (baby != null)
                {
                    yield return Until(() => ByParent() == null, 3f);
                    Check(ByParent() == null, $"its foal came through the door with it, by it on the building's floor ({ByParent() ?? "yes"})");
                }
                yield return ThroughDoor(outside, indoors: false, riddenFoals: true);
                Check(doorOk && Mounts.IsMounted, $"and back out, still riding ({reason ?? "riding"})");
                off = Mounts.IsMounted ? Beside(plain) : "not ridden";
                Check(off == null, $"outside, the mount is under the keeper again ({off ?? "yes"})");
                if (baby != null)
                {
                    yield return Until(() => ByParent() == null, 3f);
                    Check(ByParent() == null, $"and its foal came back out with it ({ByParent() ?? "yes"})");
                    baby.Remove();
                }
                else
                {
                    Warn("no foal could be spawned for the ridden mount: the foal-through-the-door part skipped");
                }
                Mounts.Dismount("self-test");
                yield return Frames(2);

                // driving, the cart not allowed: the cart stays at the door outside, the ride goes on inside
                r = carted.Ride(showMessage: false);
                Check(r.Ok && Mounts.Style == MountStyle.Cart, $"the cart driven ({Said(r)})");
                if (!r.Ok) yield break;
                yield return Frames(3);
                outside = Keeper();
                reason = null;
                var had = Carts();
                yield return ThroughDoor(inside, indoors: true);
                MountHandle left = NewCart(had);
                Check(doorOk && Mounts.IsMounted && Mounts.Style == MountStyle.Ride && !carted.HasCart, $"driving through the door, the cart not allowed: the ride goes on inside without it ({(Mounts.IsMounted ? Mounts.Style.ToString() : "off: " + reason)})");
                off = Mounts.IsMounted ? Beside(carted) : "not ridden";
                Check(off == null, $"inside, the mount is under the keeper, on the building's floor ({off ?? "yes"})");
                Check(left != null && Flat(left.I.Position, outside) < 3f, $"the cart stays outside, at the door ({(left == null ? "none left" : left.I.Position.ToString("F1"))}, the door at {outside:F1})");
                yield return ThroughDoor(outside, indoors: false);
                if (!doorOk) yield break;
                Check(Mounts.IsMounted, $"and back out, still riding ({reason ?? "riding"})");
                Mounts.Dismount("self-test");
                yield return Frames(2);

                // both on: the cart comes in
                sw.Set(true, true);
                r = carted2.Ride(showMessage: false);
                Check(r.Ok && Mounts.Style == MountStyle.Cart, $"both on: the cart driven ({Said(r)})");
                if (!r.Ok) yield break;
                yield return Frames(3);
                outside = Keeper();
                reason = null;
                yield return ThroughDoor(inside, indoors: true);
                Check(doorOk && Mounts.IsMounted && Mounts.Style == MountStyle.Cart, $"both on: driving through the door, still driving inside ({reason ?? "driving"})");
                off = Mounts.IsMounted ? Beside(carted2) : "not driven";
                Check(off == null, $"inside, the mount is under the keeper, on the building's floor ({off ?? "yes"})");
                Vector3? cart = carted2.I.TestCartBodyWorld();
                Check(cart != null && Mathf.Abs(cart.Value.y - Keeper().y) < 0.8f && Flat(cart.Value, Keeper()) < 4f, $"inside, the cart came in on the same floor ({(cart == null ? "no cart" : cart.Value.ToString("F1"))}, the keeper at {Keeper():F1})");
                yield return ThroughDoor(outside, indoors: false);
                Check(doorOk && Mounts.IsMounted && Mounts.Style == MountStyle.Cart, $"and back out, still driving ({reason ?? "driving"})");
                off = Mounts.IsMounted ? Beside(carted2) : "not driven";
                Check(off == null, $"outside, the mount is under the keeper again ({off ?? "yes"})");
            }
            finally
            {
                Mounts.Dismounted -= onDismounted;
            }
        }

        // ------------------------------------------------------------------ the morgue, mount only

        // Riding allowed indoors, the cart not, a corpse on the cart: through the morgue door the cart stays at the door,
        // the keeper gets off inside and takes the corpse into his hands, the mount inside with him (the user,
        // 2026-10-21). Then a door that isn't the morgue: the ride goes on and the corpse stays on the cart. The door
        // is the indoor light plus the door note the morgue's door would leave (MorgueHandover.LastDoorId): the real
        // door's id would reach the game's quest triggers.
        private static IEnumerator IndoorsMorgue()
        {
            string notHere = WhyNotFromHere();
            if (notHere != null)
            {
                Skip(notHere);
                yield break;
            }
            PlayerData pd = PC.PlayerData;
            if (pd.HasOverheadItem)
            {
                Warn("the keeper holds something: skipped (run it with empty hands)");
                yield break;
            }
            Vector3 near = Spot(home, 3f) ?? home;
            MountHandle a = Spawn(near, right: homeFacing > 0f, withCart: true);
            MountHandle b = Spawn(near + new Vector3(0f, 0f, 2.5f), right: homeFacing > 0f, withCart: true);
            if (a == null || b == null)
            {
                Fail("could not spawn");
                yield break;
            }
            var sw = new IndoorSwitches();
            const string MorgueDoor = "tp_RT_morgue_exit";
            string intoOwner = Mounts.Behaviour.OverriddenBy("CorpseIntoMorgue"), placesOwner = Mounts.Behaviour.OverriddenBy("CorpseIntoMorguePlaces");
            object intoWas = intoOwner == null ? null : Mounts.Behaviour.Get<object>("CorpseIntoMorgue");
            object placesWas = placesOwner == null ? null : Mounts.Behaviour.Get<object>("CorpseIntoMorguePlaces");
            var handsBefore = new HashSet<string>();
            foreach (Item it in pd.OverheadItems) handsBefore.Add(it.UniqueId.ToString());
            string reason = null;
            Action<string> onDismounted = r => reason = r;
            Mounts.Dismounted += onDismounted;
            bool lit = false;
            int bodies0 = MainGame.PlayerData.GetResInt("cur_bodies_count");
            try
            {
                yield return Until(() => a.IsReady && b.IsReady && a.I.CargoRig.Inv != null && b.I.CargoRig.Inv != null, 6f);
                if (a.I.CargoRig.Inv == null || b.I.CargoRig.Inv == null)
                {
                    Fail("the test carts have no inventory");
                    yield break;
                }
                sw.Set(true, false);
                Mounts.Behaviour.ClearAny("CorpseIntoMorgue");
                Mounts.Behaviour.ClearAny("CorpseIntoMorguePlaces");
                Mounts.Behaviour.Set(Owner, "CorpseIntoMorgue", true);
                Mounts.Behaviour.Set(Owner, "CorpseIntoMorguePlaces", MorgueDoor);
                Check(a.I.CargoRig.Inv.AddItemToInventory(NewBody()) && b.I.CargoRig.Inv.AddItemToInventory(NewBody()), "a body on each test cart");
                yield return Frames(1);

                // the morgue
                MountResult r = a.Ride(showMessage: false);
                Check(r.Ok && Mounts.Style == MountStyle.Cart, $"the cart driven with a corpse aboard ({Said(r)})");
                if (!r.Ok) yield break;
                yield return Frames(3);
                Vector3? stood = a.I.TestCartBodyWorld();
                var had = Carts();
                reason = null;
                Mount.MorgueHandover.LastDoorId = MorgueDoor; // what the morgue's door leaves (NoteTeleport)
                Mount.MorgueHandover.LastDoorAt = Time.unscaledTime;
                lit = true;
                yield return LightIndoors(true);
                yield return Until(() => pd.HasOverheadItem, 3f);
                MountHandle left = NewCart(had);
                bool held = false;
                foreach (Item it in pd.OverheadItems) if (it.id == Cargo.CorpseId && !handsBefore.Contains(it.UniqueId.ToString())) held = true;
                Check(!Mounts.IsMounted && reason == "morgue", $"at the morgue: the keeper got off inside (\"{reason ?? "still riding"}\")");
                Check(held, $"the corpse is in his hands ({pd.OverheadCount} held)");
                Check(left != null && stood != null && Flat(left.I.Position, stood.Value) < 1.5f && left.I.CargoRig.Count == 0, $"the cart stays at the door, the corpse off it ({(left == null ? "none left" : left.I.Position.ToString("F1") + ", " + left.I.CargoRig.Count + " on it")})");
                Check(a.Exists && !a.HasCart && Flat(a.I.Position, Keeper()) < 2.5f, $"the mount is inside by him ({a.I.Position:F1}, the keeper at {Keeper():F1})");
                Check(MainGame.PlayerData.GetResInt("cur_bodies_count") == bodies0, "the bodies count is unchanged");
                foreach (Item it in new List<Item>(pd.OverheadItems)) if (!handsBefore.Contains(it.UniqueId.ToString())) pd.RemoveOverheadItem(it);

                // another building: the ride goes on, the corpse stays on the cart
                yield return LightIndoors(false);
                r = b.Ride(showMessage: false);
                Check(r.Ok && Mounts.Style == MountStyle.Cart, $"another cart driven with a corpse aboard ({Said(r)})");
                if (!r.Ok) yield break;
                yield return Frames(3);
                had = Carts();
                reason = null;
                Mount.MorgueHandover.LastDoorId = "gk2donkeyapi-selftest-house";
                Mount.MorgueHandover.LastDoorAt = Time.unscaledTime;
                yield return LightIndoors(true);
                yield return Seconds(1f); // the decision: a moment after control is his
                left = NewCart(had);
                Check(Mounts.IsMounted && Mounts.Style == MountStyle.Ride && !pd.HasOverheadItem, $"not the morgue: the ride goes on, hands empty ({(Mounts.IsMounted ? Mounts.Style.ToString() : "off: " + reason)})");
                Check(left != null && Mount.MorgueHandover.HasCorpse(left.I) && !Mount.MorgueHandover.Armed, $"the corpse stays on the cart at the door ({(left == null ? "none left" : left.I.CargoRig.Count + " on it")})");
            }
            finally
            {
                Mounts.Dismounted -= onDismounted;
                if (Mounts.IsMounted) Mounts.Dismount("self-test");
                sw.Restore();
                Mounts.Behaviour.ClearAny("CorpseIntoMorgue");
                Mounts.Behaviour.ClearAny("CorpseIntoMorguePlaces");
                if (intoOwner != null && intoWas != null) Mounts.Behaviour.Set(intoOwner, "CorpseIntoMorgue", intoWas);
                if (placesOwner != null && placesWas != null) Mounts.Behaviour.Set(placesOwner, "CorpseIntoMorguePlaces", placesWas);
                Mount.MorgueHandover.LastDoorId = null;
                Mount.MorgueHandover.LastDoorAt = -1f;
                if (lit) EnvironmentEngine.Instance.SetTimeOfDayPreset(startPreset);
                int cleared = 0;
                foreach (Item it in new List<Item>(pd.OverheadItems))
                {
                    if (!handsBefore.Contains(it.UniqueId.ToString())) { pd.RemoveOverheadItem(it); cleared++; }
                }
                Check(pd.OverheadCount == handsBefore.Count, $"the keeper's hands are as they were ({cleared} test body(ies) taken out of them)");
                foreach (MountHandle h in Mounts.InstancesOf(Owner)) if (h.Exists) h.Remove(); // the test bodies go with their carts
            }
        }

        // ------------------------------------------------------------------ babies on foot

        // Babies follow the keeper on foot outdoors (the user, 2026-10-22): a foal with ShuffleFollowKeeperOnFoot follows
        // him while he walks (trots after him when he moves away); one without (another mod's foal) keeps to its parent
        // all along. He gets on their parent: they keep to it (ridden); off again: with him. He gets on another mount:
        // back to their parent. Into the indoor light with "come indoors" off: back to the parent, with him again
        // outside; with it on: they stay with him. The switch off: back to the parent.
        private static IEnumerator FoalsOnFoot()
        {
            string notHere = WhyNotFromHere();
            if (notHere != null)
            {
                Skip(notHere);
                yield break;
            }
            SaddleUpBabiesRows();
            Vector3 near = Spot(home, 3f) ?? home;
            MountHandle p = Spawn(near, right: true);
            MountHandle other = Spawn(near + new Vector3(0f, 0f, 3f), right: true);
            if (p == null || other == null)
            {
                Fail("could not spawn");
                yield break;
            }
            bool lit = false;
            MountHandle follows = null, stays = null;
            try
            {
                yield return Until(() => p.IsReady && other.IsReady, 6f);
                follows = Mounts.SpawnFoal(Owner, p, 0.6f, persist: false);
                stays = Mounts.SpawnFoal(Owner, p, 0.6f, persist: false);
                if (follows == null || stays == null)
                {
                    Fail("could not spawn the foals");
                    yield break;
                }
                yield return Until(() => follows.IsReady && stays.IsReady, 6f);
                follows.Tuning.Set(Owner, "ShuffleFollowKeeperOnFoot", true);
                string StaysNote() => $"the other foal {(stays.IsWithKeeper ? "is with the keeper" : "keeps to its parent")}";

                // on foot, outdoors
                yield return Until(() => follows.IsWithKeeper, 3f);
                Check(follows.IsWithKeeper && !stays.IsWithKeeper, $"on foot outdoors: the foal with the switch follows the keeper; {StaysNote()} (another mod's foal: untouched)");
                Vector3? walk = Spot(Keeper(), 7f, q => Flat(q, p.Position) > 6f);
                if (walk.HasValue)
                {
                    yield return MoveKeeper(walk.Value, homeFacing);
                    if (moved)
                    {
                        yield return Until(() => Flat(follows.Position, Keeper()) < 3f, 12f);
                        Check(waited, $"the keeper walks off: it comes after him ({Flat(follows.Position, Keeper()):F1} from him)");
                        Check(Flat(stays.Position, p.Position) < 4f && !stays.IsWithKeeper, $"{StaysNote()}, by it ({Flat(stays.Position, p.Position):F1})");
                    }
                }
                else
                {
                    Info("no ground 7 units off to walk to: the walk part skipped");
                }

                // on their parent, off again, on another mount (from where the run stands, by them)
                yield return MoveKeeper(home, homeFacing);
                MountResult r = p.Ride(showMessage: false);
                Check(r.Ok, $"their parent ridden ({Said(r)})");
                if (r.Ok)
                {
                    yield return Until(() => !follows.IsWithKeeper, 3f);
                    Check(!follows.IsWithKeeper, "riding their parent: it keeps to the parent (ridden, so with the keeper all the same)");
                    Mounts.Dismount("self-test");
                    yield return Until(() => follows.IsWithKeeper, 3f);
                    Check(follows.IsWithKeeper, "off again: it follows the keeper on foot");
                }
                r = other.Ride(showMessage: false);
                Check(r.Ok, $"another mount ridden ({Said(r)})");
                if (r.Ok)
                {
                    yield return Until(() => !follows.IsWithKeeper, 3f);
                    Check(!follows.IsWithKeeper, "riding another mount: it goes back to its parent");
                    Mounts.Dismount("self-test");
                    yield return Until(() => follows.IsWithKeeper, 3f);
                    Check(follows.IsWithKeeper, "off again: it follows the keeper on foot");
                }

                // indoors, without and with "come indoors"
                lit = true;
                yield return LightIndoors(true);
                yield return Until(() => !follows.IsWithKeeper, 3f);
                Check(!follows.IsWithKeeper, "indoors without \"come indoors\": it goes back to its parent");
                yield return LightIndoors(false);
                yield return Until(() => follows.IsWithKeeper, 3f);
                Check(follows.IsWithKeeper, "outdoors again: it follows the keeper");
                follows.Tuning.Set(Owner, "ShuffleFollowKeeperIndoors", true);
                yield return LightIndoors(true);
                yield return Seconds(2.2f); // two steady checks
                Check(follows.IsWithKeeper, "indoors with \"come indoors\": it stays with the keeper");
                yield return LightIndoors(false);
                lit = false;

                // the switch off
                follows.Tuning.Clear(Owner, "ShuffleFollowKeeperOnFoot");
                yield return Until(() => !follows.IsWithKeeper, 3f);
                Check(!follows.IsWithKeeper, "the switch off: it goes back to its parent");
                Check(!stays.IsWithKeeper, $"and the other foal never followed the keeper ({StaysNote()})");
            }
            finally
            {
                if (Mounts.IsMounted) Mounts.Dismount("self-test");
                if (lit) EnvironmentEngine.Instance.SetTimeOfDayPreset(startPreset);
                foreach (MountHandle h in Mounts.InstancesOf(Owner)) if (h.Exists) h.Remove();
            }
        }

        // Saddle Up's babies rows in a section of their own, the new one among them, each reaching the companion.
        private static void SaddleUpBabiesRows()
        {
            MountSettingsPage saddle = MountSettingsPage.All.Find(pg => pg.OwnerId == "dreas.gk2.saddleup" && pg.Shown);
            if (saddle == null)
            {
                Info("Saddle Up's page isn't shown: its babies rows not checked");
                return;
            }
            var keys = new[] { "Foals", "FoalsFollow", "FoalsIndoors", "FoalsWearSkin" };
            var where = new List<string>();
            bool all = true;
            foreach (string k in keys)
            {
                MountSettingsPage.Row row = saddle.Rows.Find(x => x.Entry != null && x.Entry.Definition.Key == k);
                all &= row != null && row.ShownSection == "Mount" && row.IsLook; // the mount's own, in the Mounts panel's journal (2026-10-26)
                where.Add(k + " " + (row == null ? "missing" : row.ShownSection));
            }
            Check(all, $"Saddle Up's babies rows are the mount's, in the Mounts panel's journal ({string.Join(", ", where)})");
            MountSettingsPage.Row follow = saddle.Rows.Find(x => x.Entry != null && x.Entry.Definition.Key == "FoalsFollow");
            MountCompanion me = Mounts.Companion("dreas.gk2.saddleup");
            bool on = follow?.Entry is BepInEx.Configuration.ConfigEntry<bool> fe && fe.Value;
            Check(follow != null && me.LookOf(MountCatalog.DonkeyId).FoalsFollowOnFoot == on, $"\"Babies follow you on foot\" reaches the donkey's own ({(on ? "on" : "off")})");
            Check(follow?.Entry != null && Equals(follow.Entry.DefaultValue, true), $"and it is on by default in Saddle Up ({follow?.Entry?.DefaultValue})");
        }
    }
}
