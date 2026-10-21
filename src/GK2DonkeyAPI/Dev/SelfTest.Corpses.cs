using System.Collections;
using System.Collections.Generic;
using GK2DonkeyAPI.Api;
using LazyBearTechnology;
using UnityEngine;

namespace GK2DonkeyAPI
{
    // Corpses at the crossroads (Corpses.cs, docs/corpse-delivery.md) as self-tests. They call the mod's drop step
    // directly, not the donkey's graph, so the game's body count never changes (checked at the end); every test body
    // is removed again. The switch scenario turns the feature on and off through the API and checks the patches
    // follow; the notification scenario needs it on and turns it off again. Nothing is saved.
    internal static partial class SelfTest
    {
        private static readonly List<DropData> corpseDrops = new List<DropData>();

        // A key's override as it was before a test set its own (one slot per key: setting ours would replace, say,
        // Saddle Up's switch), so it can be put back exactly.
        private readonly struct Override
        {
            public readonly string Key, Owner;
            public readonly object Value;
            public Override(string key) { Key = key; Owner = Mounts.Corpses.OverriddenBy(key); Value = Owner == null ? null : Mounts.Corpses.Get<object>(key); }
            public void Restore()
            {
                Mounts.Corpses.ClearAny(Key);
                if (Owner != null) Mounts.Corpses.Set(Owner, Key, Value);
            }
        }

        private static GDPointData GdPoint(string id) => MainGame.Instance.GameSave.worldData.gdPointsData.GetGDPointDataById(id);

        // A body the way the game's "Generate Body" node makes one for the player's tier.
        private static Item NewBody()
        {
            int tier = MainGame.PlayerData.GetResInt("bodies_tier");
            BodyDef pick = null;
            foreach (BodyDef def in GameBalance.Me.bodyDefs)
            {
                if (def.tier == tier) { pick = def; break; }
            }
            pick = pick ?? GameBalance.Me.GetDataOrNull<BodyDef>("body_0_1") ?? GameBalance.Me.bodyDefs[0];
            return pick.GenerateItem();
        }

        private static DropData FindDrop(Item item)
        {
            foreach (GameSceneData scene in MainGame.Instance.GameSave.worldData.gameSceneDataList)
            {
                foreach (DropData d in scene.droppedItems) if (d.Item == item || d.UniqueId == item.UniqueId) return d;
                foreach (DropData d in scene.queuedDrops) if (d.Item == item || d.UniqueId == item.UniqueId) return d;
            }
            return null;
        }

        private static DropView FindDropView(Item item)
        {
            foreach (GameScene scene in LazySingleton<GameSceneManager>.Instance.LoadedGameScenes)
            {
                if (scene.TryGetDropView(item, out DropView v) && v != null) return v;
            }
            return null;
        }

        private static int CountAllDrops()
        {
            int n = 0;
            foreach (GameSceneData scene in MainGame.Instance.GameSave.worldData.gameSceneDataList) n += scene.droppedItems.Count + scene.queuedDrops.Count;
            return n;
        }

        private static int CountNotifications<T>(string text) where T : UIBaseNotification
        {
            int n = 0;
            foreach (UIBaseNotification note in LazySingleton<UINotificator>.Instance.DisplayingItems)
            {
                if (note is T t && t.gameObject.activeSelf && (text == null || (t as UISimpleTextWithIconNotification)?.Text == text)) n++;
            }
            return n;
        }

        // Every test body out of the world again (the game's own removal), the count checked.
        private static IEnumerator RemoveCorpseDrops(int countBefore)
        {
            foreach (DropData d in corpseDrops)
            {
                DropData live = FindDrop(d.Item);
                if (live != null) MainGame.Instance.dropSystem.RemoveDrop(live, live.WorldId);
            }
            yield return Frames(2);
            int left = 0;
            foreach (DropData d in corpseDrops) if (FindDrop(d.Item) != null) left++;
            Check(left == 0, $"{corpseDrops.Count} test bodies removed again, none left");
            Check(MainGame.PlayerData.GetResInt("cur_bodies_count") == countBefore, $"cur_bodies_count unchanged ({countBefore})");
            corpseDrops.Clear();
            Corpses.ResetWarnings();
        }

        private static IEnumerator CorpsePoints()
        {
            GDPointData drop = GdPoint(Corpses.DropPoint.Value);
            Check(drop != null, $"drop point {Corpses.DropPoint.Value} exists{(drop == null ? "" : $" at {drop.Position:F1} in {drop.GameSceneDataId}")}");
            foreach (string id in new[] { "nun_body_drop_point", "crossroad_nun_corpses", "donkey_01", "donkey_01_01", "donkey_02", "RT_spot", "RT_body_spawn", "RT_morgue_body_spawn" })
            {
                GDPointData p = GdPoint(id);
                Check(p != null, $"GD point {id} exists{(p == null ? "" : $" at {p.Position:F1}")}");
            }
            GDPointData spawn = GdPoint("donkey_01"), road = GdPoint("donkey_01_01"), nun = GdPoint("nun_body_drop_point");
            if (spawn != null && road != null && nun != null)
            {
                Check(Vector3.Distance(spawn.Position, road.Position) < 5f, $"the walk out is short ({Vector3.Distance(spawn.Position, road.Position):F1} units)");
                Check(Vector3.Distance(road.Position, nun.Position) < 8f, $"the stand point is by the drop point ({Vector3.Distance(road.Position, nun.Position):F1} units)");
            }
            Info(Corpses.DescribeMorgue());
            yield break;
        }

        private static IEnumerator CorpseOneBody()
        {
            int countBefore = MainGame.PlayerData.GetResInt("cur_bodies_count");
            GDPointData point = GdPoint(Corpses.DropPoint.Value);
            if (point == null) { Fail("no drop point"); yield break; }
            Item body = NewBody();
            int parts = body.Inventory.Count;
            Check(body.Count == 1 && parts > 0, $"generated {body.id} with {parts} items inside (parts, pockets, reward)");
            CorpseInfo seen = null;
            System.Action<CorpseInfo> onLeft = c => seen = c;
            Mounts.CorpseLeft += onLeft;
            bool ok;
            try
            {
                ok = Corpses.TryLeaveAtCrossroads(body);
            }
            finally
            {
                Mounts.CorpseLeft -= onLeft;
            }
            Check(ok, "the drop step took it");
            if (!ok) yield break;
            Check(seen != null && seen.Id == body.id && seen.Point == point.Id, $"CorpseLeft raised ({seen})");
            Check(Corpses.TakeRedirectedNotification(), "one notification is pending for it (same frame)");
            Check(!Corpses.TakeRedirectedNotification(), "and only one");
            DropData drop = FindDrop(body);
            Check(drop != null, "it is a drop in the point's scene");
            if (drop == null) yield break;
            corpseDrops.Add(drop);
            Check(ReferenceEquals(drop.Item, body), "the very same item instance (nothing rebuilt)");
            Check(drop.Item.Inventory.Count == parts, $"its inside is intact ({drop.Item.Inventory.Count} items)");
            Check(drop.DropType == DropType.Item, $"a plain item drop ({drop.DropType})");
            // The game nudges a new drop a little on placement (ground snap, separation).
            Check(Vector3.Distance(drop.Position, point.Position) < 1f, $"placed at the point ({Vector3.Distance(drop.Position, point.Position):F2} from it)");
            Check(drop.AutoDestroyTimer > 0f, $"the game's rot timer started ({drop.AutoDestroyTimer:F0} s), as for its own ground bodies");
            yield return Frames(3);
            DropView view = FindDropView(body);
            if (view != null) Pass($"it has a view at {view.transform.position:F1}");
            else if (Vector3.Distance(Keeper(), point.Position) > 60f) Warn("no view (the keeper is far from the crossroads; the scene may not draw it there)");
            else Fail("no view although the keeper is near");
            yield return RemoveCorpseDrops(countBefore);
        }

        private static IEnumerator CorpseSwitch()
        {
            bool wasOn = Corpses.AtCrossroads.Value;
            Check(!Corpses.Applied || wasOn, $"patched only while on (on {wasOn}, patched {Corpses.Applied})");
            var before = new Override("CorpsesAtCrossroads");
            Info(before.Owner == null ? $"the switch is the player's cfg value ({wasOn})" : $"the switch is set by {before.Owner} ({wasOn})");
            var pallet = HarmonyLib.AccessTools.DeclaredMethod(typeof(GK2.FlowCanvasNodes.Flow_TryToAddCorpseToPallet), "TryToAddCorpseToPallet");
            try
            {
                Mounts.Corpses.ClearAny("CorpsesAtCrossroads"); // Saddle Up holds it: taken over, put back by before.Restore()
                Mounts.Corpses.Set(Owner, "CorpsesAtCrossroads", true);
                Corpses.Tick();
                Check(Corpses.Applied && Mounts.CorpsesActive, "switched on: the patches are in" + (Corpses.ApplyError != null ? " (" + Corpses.ApplyError + ")" : ""));
                var info = HarmonyLib.Harmony.GetPatchInfo(pallet);
                Check(info != null && System.Linq.Enumerable.Any(info.Prefixes, x => x.owner == Plugin.Guid + ".corpses"), "our prefix is on the pallet node");
                Mounts.Corpses.Set(Owner, "CorpsesAtCrossroads", false);
                Corpses.Tick();
                info = HarmonyLib.Harmony.GetPatchInfo(pallet);
                Check(!Corpses.Applied && (info == null || !System.Linq.Enumerable.Any(info.Prefixes, x => x.owner == Plugin.Guid + ".corpses")), "switched off: the patches are gone, the game's donkey code is as it was");
            }
            finally
            {
                before.Restore();
                Corpses.Tick();
            }
            Check(Corpses.AtCrossroads.Value == wasOn && Corpses.Applied == (Plugin.Enabled.Value && wasOn), "back to the setting as it was");
            yield break;
        }

        private static IEnumerator CorpseNotification()
        {
            int countBefore = MainGame.PlayerData.GetResInt("cur_bodies_count");
            UINotificator notificator = LazySingleton<UINotificator>.Instance;
            if (notificator == null) { Fail("no UINotificator"); yield break; }
            var before = new Override("CorpsesAtCrossroads");
            Mounts.Corpses.ClearAny("CorpsesAtCrossroads");
            Mounts.Corpses.Set(Owner, "CorpsesAtCrossroads", true);
            Corpses.Tick();
            if (!Corpses.Applied) { Fail("could not switch the feature on"); before.Restore(); Corpses.Tick(); yield break; }
            try
            {
                string text = Corpses.Notification.Value;
                Item body = NewBody();
                if (!Corpses.TryLeaveAtCrossroads(body)) { Fail("drop step refused"); yield break; }
                DropData drop = FindDrop(body);
                if (drop != null) corpseDrops.Add(drop);
                int oursBefore = CountNotifications<UISimpleTextWithIconNotification>(text), stockBefore = CountNotifications<UINewBodyNotification>(null);
                notificator.ShowNewBodyNotification("body_0_1"); // what the graph's node calls, same frame as the redirect
                if (string.IsNullOrWhiteSpace(text))
                {
                    Check(CountNotifications<UINewBodyNotification>(null) == stockBefore + 1, "empty text: the game's own New Body notification shows");
                }
                else
                {
                    Check(CountNotifications<UISimpleTextWithIconNotification>(text) == oursBefore + 1, $"our notification shows (\"{text}\")");
                    Check(CountNotifications<UINewBodyNotification>(null) == stockBefore, "and the game's New Body does not");
                }
                yield return null; // next frame: nothing pending any more
                stockBefore = CountNotifications<UINewBodyNotification>(null);
                notificator.ShowNewBodyNotification("body_0_1");
                Check(CountNotifications<UINewBodyNotification>(null) == stockBefore + 1, "without a body of ours the game's own notification shows (the nun's script path)");
                string icon = Corpses.IconFor("body_0_1");
                Check(!string.IsNullOrEmpty(icon) && LazySingletonSO<EasySpritesCollection>.Instance.GetSprite(icon) != null, $"the icon resolves ({icon})");
            }
            finally
            {
                before.Restore();
                Corpses.Tick();
            }
            yield return RemoveCorpseDrops(countBefore);
        }

        private static IEnumerator CorpseManyBodies()
        {
            const int n = 12;
            int countBefore = MainGame.PlayerData.GetResInt("cur_bodies_count");
            GDPointData point = GdPoint(Corpses.DropPoint.Value);
            if (point == null) { Fail("no drop point"); yield break; }
            var bodies = new List<Item>();
            int took = 0;
            for (int i = 0; i < n; i++)
            {
                Item body = NewBody();
                bodies.Add(body);
                if (Corpses.TryLeaveAtCrossroads(body)) took++;
            }
            Check(took == n, $"{took} of {n} bodies taken in one frame (the graph's loop delivers them like this)");
            int pending = 0;
            while (Corpses.TakeRedirectedNotification()) pending++;
            Check(pending == took, $"{pending} notifications pending, one per body");
            var drops = new List<DropData>();
            foreach (Item b in bodies)
            {
                DropData d = FindDrop(b);
                if (d != null) { drops.Add(d); corpseDrops.Add(d); }
            }
            Check(drops.Count == took, $"{drops.Count} drops in the scene, all count 1: {drops.TrueForAll(d => d.Count == 1)}");
            yield return Seconds(1.5f); // the views settle and push apart
            var views = new List<DropView>();
            foreach (Item b in bodies)
            {
                DropView v = FindDropView(b);
                if (v != null) views.Add(v);
            }
            if (views.Count == 0 && Vector3.Distance(Keeper(), point.Position) > 60f)
            {
                Warn("no views (the keeper is far from the crossroads); positions not judged");
            }
            else
            {
                Check(views.Count == took, $"{views.Count} views");
                int tooClose = 0, far = 0, sunk = 0;
                for (int i = 0; i < views.Count; i++)
                {
                    Vector3 p = views[i].transform.position;
                    if (Vector3.Distance(p, point.Position) > 8f) far++;
                    if (p.y < point.Position.y - 1f || p.y > point.Position.y + 2.5f) sunk++;
                    for (int j = i + 1; j < views.Count; j++)
                    {
                        if (Vector3.Distance(p, views[j].transform.position) < 0.3f) tooClose++;
                    }
                }
                Check(tooClose == 0, $"no two bodies on the same spot ({tooClose} pairs closer than 0.3)");
                Check(far == 0, $"all within 8 of the point ({far} further)");
                Check(sunk == 0, $"all at ground level ({sunk} off in height)");
            }
            int still = 0;
            foreach (DropData d in drops) if (FindDrop(d.Item) != null) still++;
            Check(still == drops.Count, $"all {still} still separate drops after settling (none merged, none lost)");
            yield return RemoveCorpseDrops(countBefore);
        }

        private static IEnumerator CorpseNeverRot()
        {
            int countBefore = MainGame.PlayerData.GetResInt("cur_bodies_count");
            var before = new Override("CorpsesNeverRot");
            Item body;
            bool ok;
            try
            {
                Mounts.Corpses.ClearAny("CorpsesNeverRot");
                Mounts.Corpses.Set(Owner, "CorpsesNeverRot", true);
                body = NewBody();
                ok = Corpses.TryLeaveAtCrossroads(body);
            }
            finally
            {
                before.Restore();
            }
            Check(ok, "drop step took it");
            DropData drop = FindDrop(body);
            if (drop == null) { Fail("no drop"); yield break; }
            corpseDrops.Add(drop);
            Corpses.TakeRedirectedNotification();
            Check(body.HasProperty<NeverAutoDestroyDropSerializedItemProperty>(), "the game's never-auto-destroy property is on the body");
            Check(drop.CanNotBeAutoDestroyed.ResultFlag, "the drop is flagged as never auto-destroyed");
            if (!Corpses.NeverRot.Value)
            {
                Item plain = NewBody();
                if (Corpses.TryLeaveAtCrossroads(plain))
                {
                    Corpses.TakeRedirectedNotification();
                    DropData d2 = FindDrop(plain);
                    if (d2 != null) corpseDrops.Add(d2);
                    Check(d2 != null && !plain.HasProperty<NeverAutoDestroyDropSerializedItemProperty>(), "with the setting off a body gets no such property");
                }
            }
            yield return RemoveCorpseDrops(countBefore);
        }

        private static IEnumerator CorpseUnknownPoint()
        {
            Item body = NewBody();
            int dropsBefore = CountAllDrops();
            bool ok = Corpses.TryLeaveAt(body, "gk2_donkeyapi_no_such_point");
            Check(!ok, "the drop step steps aside (the game's own delivery would run)");
            Check(CountAllDrops() == dropsBefore, "and nothing was dropped");
            Check(!Corpses.TakeRedirectedNotification(), "and no notification is pending");
            Check(FindDrop(body) == null, "the unused body is nowhere in the world");
            Corpses.ResetWarnings();
            yield break;
        }

        private static IEnumerator CorpseWalkDecisions()
        {
            Vector3 start = new Vector3(77.6f, 3.6f, 14.65f), morgue = new Vector3(24.96f, 3.6f, 15.6f), stand = new Vector3(74.64f, 3.6f, 15.6f);
            Check(Corpses.Decide("full_cycle_back", true, start, null, null) == Corpses.WalkAction.UnloadHere, "delivery walk, no quest talk: unload at the crossroads");
            Check(Corpses.Decide("full_cycle_back", true, start, null, "21_base_donkey_nun") == Corpses.WalkAction.Vanilla, "delivery walk while a quest talk waits at the morgue: the game's walk");
            Check(Corpses.Decide("Disable", true, start, start, null) == Corpses.WalkAction.LeaveHere, "walk back to where it already stands (unloaded on the spot): leave from here");
            Check(Corpses.Decide("Disable", true, stand, start, null) == Corpses.WalkAction.Vanilla, "walk back from the stand point: the game's walk (it vanishes at its spawn point)");
            Check(Corpses.Decide("Disable", true, morgue, start, null) == Corpses.WalkAction.Vanilla, "walk back from the morgue (after a talk): the game's walk");
            Check(Corpses.Decide("Disable", true, start, null, null) == Corpses.WalkAction.Vanilla, "walk back with no known target: the game's walk");
            Check(Corpses.Decide("stay_morgue", true, start, null, null) == Corpses.WalkAction.Vanilla, "any other walk of the donkey: the game's walk");
            Check(Corpses.Decide("full_cycle_back", false, start, null, null) == Corpses.WalkAction.Vanilla, "another WGO's walk: the game's walk");
            string hold = Corpses.QuestHoldingDonkeyAtMorgue();
            Info(hold == null ? "no quest holds the donkey at the morgue in this save" : $"quest {hold} holds the donkey at the morgue in this save (deliveries walk as the game's until it is done)");
            WorldData world = MainGame.Instance.GameSave.worldData;
            WgoData donkey = world.GetWgoDataByCustomTag("npc_donkey") ?? world.GetWgoData("npc_donkey");
            Check(donkey != null && Corpses.IsDonkey(donkey), $"the game's donkey is found and recognised{(donkey == null ? "" : $" (id {donkey.id}, tag {donkey.CustomTag}, at {donkey.Position:F1})")}");
            yield break;
        }
    }
}
